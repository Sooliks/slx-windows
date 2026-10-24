using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Slx.Client.Core;
using Slx.Client.Windows;

namespace Slx.Client.Service;

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
    public TunnelStatus Status => state with { Recovery = recovery, HealthCheck = healthCheck, DnsCheck = dnsCheck };

    public async Task StartAsync(ConnectPlan plan, string sid, CancellationToken cancellationToken)
    {
        PlanValidator.Validate(plan);
        var pinned = pinnedPlan is not null && currentPlan is not null
            && (plan.Profile.Id==currentPlan.Profile.Id || string.Equals(plan.Profile.Host,currentPlan.Profile.Host,StringComparison.OrdinalIgnoreCase))
            ? plan with{Profile=plan.Profile with{Host=pinnedPlan.Profile.Host}} : await PinEndpointAsync(plan, cancellationToken);
        string executable = RuntimeTrust.Verify();
        await lifecycle.WaitAsync(cancellationToken);
        try
        {
            using var work = connectionWork.Begin(cancellationToken);
            cancellationToken = work.Token;
            cancellationToken.ThrowIfCancellationRequested();
            if (offCleanup.Pending) throw new ClientFault("TUN_CLEANUP_PENDING");
            bool rollback = confirmedUnprotected && InitialConnectionAttempt.CanRollback(state, desiredConnected,
                currentPlan is not null, child is not null || ownedAdapterId is not null);
            await InitialConnectionAttempt.RunAsync(rollback, async () =>
            {
                ArmProtection(plan);
                pendingApplicationUpdate = null;
                startupOffCleanup.Cancel();
                currentPlan=plan;ownerSid=sid;desiredConnected=true;pinnedPlan=pinned;retryAt=0;
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

    public async Task ReconnectAsync(CancellationToken cancellationToken)
    {
        var plan = currentPlan ?? throw new ClientFault("NO_CONNECTION_PLAN");
        string sid = ownerSid ?? throw new ClientFault("NO_CONNECTION_PLAN");
        await StartAsync(plan, sid, cancellationToken);
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
        state = new(phase, DateTimeOffset.UtcNow, plan.Profile.Name, Mode: plan.Mode, ProtectionActive:protectionActive);
        try
        {
            if (LocalProbe.AdapterExists()) throw new ClientFault("TUN_ADAPTER_CONFLICT");
            probe = LocalProbe.ReserveCredentials();
            string config = XrayConfigBuilder.Build(plan, probe.Port, probe.User, probe.Password,
                OutboundInterface.ForEndpoint(plan.Profile.Host));
            var start = new ProcessStartInfo(executable)
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true,
                RedirectStandardOutput = true, RedirectStandardError = true,
                WorkingDirectory = Path.GetDirectoryName(executable)!
            };
            start.ArgumentList.Add("run");
            start.ArgumentList.Add("-config");
            start.ArgumentList.Add("stdin:");
            start.ArgumentList.Add("-format");
            start.ArgumentList.Add("json");
            start.Environment.Clear();
            start.Environment["PATH"] = Environment.SystemDirectory;
            start.Environment["SYSTEMROOT"] = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            start.Environment["NODE_ENV"] = "production";
            var process = new Process { StartInfo = start };
            if (!process.Start()) throw new ClientFault("CORE_START_FAILED");
            child = process;
            job = new ProcessJob(process);
            process.Exited += (_, _) => _ = HandleExitAsync(process);
            process.EnableRaisingEvents = true;
            _ = DrainAsync(process.StandardOutput.BaseStream);
            _ = DrainAsync(process.StandardError.BaseStream);
            await process.StandardInput.WriteAsync(config.AsMemory(), cancellationToken);
            process.StandardInput.Close();
            config = "";
            await LocalProbe.WaitAsync(probe.Port, probe.User, probe.Password, () => !process.HasExited, cancellationToken);
            ownedAdapterId = LocalProbe.AdapterIdentity() ?? throw new ClientFault("TUN_NOT_READY");
            TunnelSourcePolicy.Apply(ownedAdapterId);
            ArmProtection(plan,ownedAdapterId);
            DnsCache.Refresh();
            await TunnelDnsProbe.CheckAsync(ownedAdapterId, RecordDnsCheck, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (process.HasExited || !LocalProbe.TunnelReady()) throw new ClientFault("CORE_EXITED");
            state = new(TunnelPhase.Connected, DateTimeOffset.UtcNow, plan.Profile.Name,
                ConnectedAt: DateTimeOffset.UtcNow, Mode: plan.Mode, ProbePassed: true, ProtectionActive:protectionActive,
                ApplicationRevision: XrayRouteRevision.For(currentPlan ?? plan));
            pendingApplicationUpdate = null;
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

    private static async Task<ConnectPlan> PinEndpointAsync(ConnectPlan plan, CancellationToken cancellationToken)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(TimeSpan.FromSeconds(8));
        try
        {
            var addresses = await Dns.GetHostAddressesAsync(plan.Profile.Host, budget.Token);
            if (addresses.Length == 0 || addresses.Any(address => !EndpointPolicy.IsPublicAddress(address)))
                throw new ClientFault("ENDPOINT_NOT_PUBLIC");
            var selected = addresses.FirstOrDefault(address => address.AddressFamily == AddressFamily.InterNetwork) ?? addresses[0];
            return plan with { Profile = plan.Profile with { Host = selected.ToString() } };
        }
        catch (SocketException) { throw new ClientFault("ENDPOINT_UNREACHABLE"); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new ClientFault("ENDPOINT_TIMEOUT"); }
    }

    private static async Task DrainAsync(Stream stream)
    {
        try { await stream.CopyToAsync(Stream.Null); }
        catch (Exception error) when (error is IOException or ObjectDisposedException) { }
    }
}
