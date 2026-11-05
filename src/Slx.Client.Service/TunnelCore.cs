using System.Diagnostics;
using Slx.Client.Core;

namespace Slx.Client.Service;

/// <summary>The restartable VPN core. Stopping or replacing it never touches the TUN process or its routes.</summary>
internal sealed partial class TunnelEngine
{
    private readonly TransportRotation rotation = new();
    private Process? core;
    private ProcessJob? coreJob;
    private (int Port, string User, string Password) coreLink;
    private volatile ConnectionAttemptObservation? attempt;
    /// <summary>The loopback link could not be reused; the next recovery rebuilds both processes.</summary>
    private bool coreLinkSuspect;

    private bool CoreAlive => core is { HasExited: false };
    private bool FrontAlive => child is { HasExited: false };
    private XrayCoreLink CoreLink(string executable) => new(coreLink.Port, coreLink.User, coreLink.Password, executable);
    private void PublishAttempt() => attempt = rotation.Observe(DateTimeOffset.UtcNow);

    private async Task StartCoreAsync(string executable, CancellationToken cancellationToken)
    {
        if (core is not null) throw new ClientFault("CORE_START_FAILED");
        string config = XrayCoreConfig.Build(rotation.Current, CoreLink(executable));
        var process = XrayProcess.Start(executable, started => core = started);
        coreJob = new ProcessJob(process);
        process.Exited += (_, _) => _ = HandleCoreExitAsync(process);
        process.EnableRaisingEvents = true;
        await XrayProcess.SendConfigAsync(process, config, cancellationToken);
        PublishAttempt();
    }

    private async Task StopCoreAsync()
    {
        var process = core;
        core = null;
        // KILL_ON_JOB_CLOSE also ends the core if this service cannot wait for it.
        coreJob?.Dispose();
        coreJob = null;
        if (process is null) return;
        bool stopped = false;
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await process.WaitForExitAsync(budget.Token);
            stopped = true;
        }
        catch (Exception error) when (error is InvalidOperationException
            or System.ComponentModel.Win32Exception or OperationCanceledException)
        {
            stopped = process.HasExited;
            if (!stopped) { core = process; throw new ClientFault("CORE_STOP_FAILED"); }
        }
        finally { if (stopped) process.Dispose(); }
    }

    /// <summary>Core-only recovery: the TUN process, its routes, DNS and all direct flows stay untouched.</summary>
    private async Task RecoverCoreAsync(string executable, CancellationToken cancellationToken)
    {
        var plan = currentPlan ?? throw new ClientFault("NO_CONNECTION_PLAN");
        var front = child ?? throw new ClientFault("CORE_EXITED");
        state = state with { Phase = TunnelPhase.Reconnecting, ObservedAt = DateTimeOffset.UtcNow, ProbePassed = false,
            ProtectionActive = protectionActive };
        await StopCoreAsync();
        await StartCoreAsync(executable, cancellationToken);
        await LocalProbe.WaitAsync(probe.Port, probe.User, probe.Password, () => !front.HasExited && CoreAlive,
            cancellationToken, TimeSpan.FromSeconds(15));
        cancellationToken.ThrowIfCancellationRequested();
        if (front.HasExited || !CoreAlive || !LocalProbe.TunnelReady()) throw new ClientFault("CORE_EXITED");
        state = new(TunnelPhase.Connected, DateTimeOffset.UtcNow, plan.Profile.Name, ConnectedAt: DateTimeOffset.UtcNow,
            Mode: plan.Mode, ProbePassed: true, ProtectionActive: protectionActive,
            ApplicationRevision: XrayRouteRevision.For(plan));
        PublishAttempt(); rotation.Succeeded();
        recoveryAttempts = 0; healthAt = Stopwatch.GetTimestamp() + 15 * Stopwatch.Frequency;
    }

    /// <summary>A failed core-only attempt keeps the TUN process; only a broken loopback link rebuilds both.</summary>
    private async Task FailCoreRecoveryAsync(string code)
    {
        await BackgroundCleanup.TryAsync(StopCoreAsync);
        if (code is "CORE_EXITED" or "CORE_START_FAILED" or "CORE_CONTAINMENT_FAILED" or "CORE_STOP_FAILED")
            coreLinkSuspect = true;
        rotation.Failed(code);
        PublishAttempt();
        ScheduleRecovery(code);
    }

    private bool CanRecoverCoreOnly() => FrontAlive && ownedAdapterId is not null && probe.Port != 0
        && coreLink.Port != 0 && !coreLinkSuspect && LocalProbe.TunnelReady();

    private async Task HandleCoreExitAsync(Process exited)
    {
        await lifecycle.WaitAsync();
        try
        {
            if (!ReferenceEquals(core, exited)) return;
            if (state.Phase is TunnelPhase.Connected or TunnelPhase.Checking) RecordRecovery(RecoveryCause.CoreExited);
            await BackgroundCleanup.TryAsync(StopCoreAsync);
            if (!desiredConnected) return;
            rotation.Failed("CORE_EXITED");
            PublishAttempt();
            ScheduleRecovery("CORE_EXITED");
        }
        finally { lifecycle.Release(); }
    }
}
