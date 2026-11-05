using System.Diagnostics;
using Slx.Client.Core;
using Slx.Client.Windows;

namespace Slx.Client.Service;

/// <summary>
/// Two verified Xray processes: a long-lived TUN process that owns routes and sends unselected traffic directly,
/// and a restartable VPN core reached over an authenticated loopback link. Recovery and transport switches
/// restart only the core, so direct traffic of unselected applications never depends on the VPN server.
/// </summary>
internal sealed partial class TunnelEngine : IControlTunnelEngine
{
    private readonly SemaphoreSlim lifecycle = new(1, 1);
    private readonly TunnelWork connectionWork = new();
    private volatile TunnelStatus state = new(TunnelPhase.Disconnected, DateTimeOffset.UtcNow);
    private Process? child;
    private ProcessJob? job;
    private ConnectPlan? currentPlan;
    private string? ownerSid;
    private string? ownedAdapterId;
    private (int Port, string User, string Password) probe;
    private int recoveryAttempts;
    public TunnelStatus Status => state with { Recovery = recovery, HealthCheck = healthCheck, DnsCheck = dnsCheck,
        TrafficRoutes = trafficRoutes, Attempt = desiredConnected ? attempt : null };

    public Task StartAsync(ConnectPlan plan, string sid, CancellationToken cancellationToken) =>
        ConnectAsync(plan, sid, repin: false, cancellationToken);

    private async Task ConnectAsync(ConnectPlan plan, string sid, bool repin, CancellationToken cancellationToken)
    {
        plan = ExactApplicationPlan.Prepare(plan, cancellationToken);
        var pinned = !repin && pinnedPlan is not null && currentPlan is not null
            && (plan.Profile.Id==currentPlan.Profile.Id || string.Equals(plan.Profile.Host,currentPlan.Profile.Host,StringComparison.OrdinalIgnoreCase))
            ? await TunnelEndpoints.PinAsync(plan with{Profile=plan.Profile with{Host=pinnedPlan.Profile.Host}}, cancellationToken, pinnedPlan)
            : await TunnelEndpoints.PinAsync(plan, cancellationToken, repin ? null : pinnedPlan);
        string executable = RuntimeTrust.Verify();
        await lifecycle.WaitAsync(cancellationToken);
        try
        {
            using var work = connectionWork.Begin(cancellationToken);
            cancellationToken = work.Token;
            cancellationToken.ThrowIfCancellationRequested();
            if (offCleanup.Pending) throw new ClientFault("TUN_CLEANUP_PENDING");
            bool rollback = confirmedUnprotected && InitialConnectionAttempt.CanRollback(state, desiredConnected,
                currentPlan is not null, child is not null || core is not null || ownedAdapterId is not null);
            await InitialConnectionAttempt.RunAsync(rollback, async () =>
            {
                ArmProtection(plan);
                pendingApplicationUpdate = null;
                startupOffCleanup.Cancel();
                currentPlan=plan;ownerSid=sid;desiredConnected=true;pinnedPlan=pinned;retryAt=0;
                rotation.Reset(pinned);
                state=new(TunnelPhase.Connecting,DateTimeOffset.UtcNow,plan.Profile.Name,Mode:plan.Mode,ProtectionActive:protectionActive);
                try{intents.Save(new(sid,pinned,plan.Profile.Host));await StopChildAsync();}
                catch(Exception error)
                {
                    string code=error is ClientFault fault?fault.Code:"PROTECTION_STORAGE_FAILED";
                    ScheduleRecovery(code);
                    if(error is OperationCanceledException)throw;
                    throw new ClientFault(code);
                }
                recoveryAttempts=0;
                await LaunchAsync(pinned, executable, TunnelPhase.Connecting, cancellationToken);
            }, async code =>
            {
                RequestOff(code);
                await ContinueOffCleanupAsync(force: true);
            }, cancellationToken);
        }
        finally { lifecycle.Release(); }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        using var stopping = connectionWork.RequestStop();
        await lifecycle.WaitAsync(cancellationToken);
        try
        {
            RequestOff();
            await ContinueOffCleanupAsync(force: true);
        }
        finally { lifecycle.Release(); }
    }

    private async Task LaunchAsync(ConnectPlan plan, string executable, TunnelPhase phase, CancellationToken cancellationToken)
    {
        dnsCheck = null;
        trafficRoutes = null;
        state = new(phase, DateTimeOffset.UtcNow, plan.Profile.Name, Mode: plan.Mode, ProtectionActive:protectionActive);
        try
        {
            if (LocalProbe.AdapterExists()) throw new ClientFault("TUN_ADAPTER_CONFLICT");
            rotation.Ensure(plan);
            probe = LocalProbe.ReserveCredentials();
            coreLink = LocalProbe.ReserveCredentials();
            coreLinkSuspect = false;
            await StartCoreAsync(executable, cancellationToken);
            string config = XrayConfigBuilder.Build(plan, probe.Port, probe.User, probe.Password,
                OutboundInterface.ForEndpoint(rotation.Current.Host), CoreLink(executable));
            var process = XrayProcess.Start(executable, started => child = started);
            job = new ProcessJob(process);
            process.Exited += (_, _) => _ = HandleExitAsync(process);
            process.EnableRaisingEvents = true;
            await XrayProcess.SendConfigAsync(process, config, cancellationToken);
            config = "";
            await WaitForVpnAsync(process, executable, phase, cancellationToken);
            ownedAdapterId = LocalProbe.AdapterIdentity() ?? throw new ClientFault("TUN_NOT_READY");
            TunnelSourcePolicy.Apply(ownedAdapterId);
            ArmProtection(plan,ownedAdapterId);
            DnsCache.Refresh();
            await TunnelDnsProbe.CheckAsync(ownedAdapterId, RecordDnsCheck, cancellationToken);
            await TrafficRouteProbe.CheckAsync(ownedAdapterId, RecordTrafficRoutes, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (process.HasExited || !CoreAlive || !LocalProbe.TunnelReady()) throw new ClientFault("CORE_EXITED");
            cancellationToken.ThrowIfCancellationRequested();
            state = new(TunnelPhase.Connected, DateTimeOffset.UtcNow, plan.Profile.Name,
                ConnectedAt: DateTimeOffset.UtcNow, Mode: plan.Mode, ProbePassed: true, ProtectionActive:protectionActive,
                ApplicationRevision: XrayRouteRevision.For(currentPlan ?? plan));
            pendingApplicationUpdate = null;
            PublishAttempt(); rotation.Succeeded();
            recoveryAttempts=0; healthAt=Stopwatch.GetTimestamp()+15*Stopwatch.Frequency;
        }
        catch (Exception error)
        {
            await StopChildAsync();
            string code = error is ClientFault fault ? fault.Code
                : error is OperationCanceledException ? "OPERATION_INTERRUPTED" : "CORE_START_FAILED";
            if(desiredConnected)ScheduleRecovery(code);
            else state = new(TunnelPhase.Faulted, DateTimeOffset.UtcNow, plan.Profile.Name, code, Mode: plan.Mode, ProtectionActive:protectionActive);
            if (error is ClientFault or OperationCanceledException) throw;
            throw new ClientFault(code);
        }
    }

    /// <summary>
    /// While the user waits for a first connection, a transport that fails its whole readiness budget
    /// (for example UDP blocked for Hysteria2) is replaced once by another transport of the same server.
    /// </summary>
    private async Task WaitForVpnAsync(Process front, string executable, TunnelPhase phase, CancellationToken cancellationToken)
    {
        bool fallback = phase == TunnelPhase.Connecting && rotation.Count > 1;
        try
        {
            await LocalProbe.WaitAsync(probe.Port, probe.User, probe.Password, () => !front.HasExited && CoreAlive,
                cancellationToken, TimeSpan.FromSeconds(fallback ? 20 : 32));
        }
        catch (ClientFault fault) when (fallback && fault.Code == "VPN_PROBE_FAILED" && !front.HasExited && LocalProbe.TunnelReady())
        {
            rotation.Advance(fault.Code);
            await StopCoreAsync();
            await StartCoreAsync(executable, cancellationToken);
            await LocalProbe.WaitAsync(probe.Port, probe.User, probe.Password, () => !front.HasExited && CoreAlive,
                cancellationToken, TimeSpan.FromSeconds(15));
        }
    }
}
