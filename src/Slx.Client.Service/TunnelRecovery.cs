using Slx.Client.Core;

namespace Slx.Client.Service;

internal sealed partial class TunnelEngine
{
    private readonly CoreRecoveryPolicy coreRecovery = new();
    private readonly TunnelLifecycleJournal lifecycleJournal = new();
    private TunnelLifecycleOrigin recoveryOrigin = TunnelLifecycleOrigin.AutomaticRecovery;
    private TunnelLifecycleReason recoveryReason = TunnelLifecycleReason.StartupResume;

    internal TunnelLifecycleOrigin TakeRecoveryOrigin()
    {
        var origin = recoveryOrigin;
        recoveryOrigin = TunnelLifecycleOrigin.AutomaticRecovery;
        return origin;
    }

    private CoreRecoveryDecision RecoveryDecision(TunnelLifecycleReason reason) => coreRecovery.Choose(
        new(FrontAlive, ownedAdapterId is not null && LocalProbe.TunnelReady(ownedAdapterId),
            probe.Port != 0, coreLink.Port != 0), reason);

    private bool CanRecoverCoreOnly() => RecoveryDecision(TunnelLifecycleReason.Requested).Scope == TunnelLifecycleScope.VpnCore;

    private async Task<RecoveryAttemptResult> RunRecoveryAsync(TunnelLifecycleOrigin origin,
        CoreRecoveryDecision decision, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // A request/restore origin belongs to this attempt, including cancellation, not a later unrelated failure.
        recoveryOrigin = TunnelLifecycleOrigin.AutomaticRecovery;
        state = state with { Phase = TunnelPhase.Reconnecting, ObservedAt = DateTimeOffset.UtcNow,
            ProbePassed = false, ProtectionActive = protectionActive };
        var attemptedTransport = rotation.Count > 0 ? TransportExecution.Candidate(rotation, DateTimeOffset.UtcNow) : null;
        var result = await RecoveryAttempt.RunAsync(decision, RuntimeTrust.Verify, RecoverCoreAsync, RecoverTunnelAsync,
            FailCoreRecoveryAsync, FailTunnelRecoveryAsync,
            (outcome, code) => lifecycleJournal.Record(origin, decision, outcome, code, attemptedTransport), cancellationToken);
        if (result.Succeeded) retryAt = 0;
        return result;
    }

    private async Task RecoverTunnelAsync(string executable, CancellationToken cancellationToken)
    {
        PrepareExactApplicationPaths(cancellationToken);
        await StopChildAsync();
        cancellationToken.ThrowIfCancellationRequested();
        ArmProtection(currentPlan!);
        var pinned = pinnedPlan ?? await TunnelEndpoints.PinAsync(currentPlan!, cancellationToken);
        intents.Save(new(ownerSid!, pinned, currentPlan!.Profile.Host));
        await LaunchAsync(pinned, executable, TunnelPhase.Reconnecting, cancellationToken);
    }

    private async Task FailTunnelRecoveryAsync(string code)
    {
        transportExecution.Failed();
        await BackgroundCleanup.TryAsync(StopChildAsync);
        if (code == "VPN_PROBE_FAILED") { rotation.Failed(code); PublishAttempt(); }
        ScheduleRecovery(code);
    }

    private async Task CheckCoreRecoveryRoutesAsync(CancellationToken cancellationToken)
    {
        if (ownedAdapterId is null) throw new ClientFault("TUN_NOT_READY");
        await TrafficRouteProbe.CheckAsync(ownedAdapterId, RecordTrafficRoutes, cancellationToken);
        if (!PhysicalPathReady() || coreRecovery.RequiredFullReason is not null)
            throw new ClientFault("OUTBOUND_INTERFACE_UNAVAILABLE");
    }

    private async Task TrackConnectionAsync(TunnelLifecycleOrigin origin, Func<Task> connect,
        CancellationToken cancellationToken)
    {
        var decision = new CoreRecoveryDecision(TunnelLifecycleScope.FullTunnel,
            coreRecovery.RequiredFullReason ?? TunnelLifecycleReason.Requested);
        cancellationToken.ThrowIfCancellationRequested();
        lifecycleJournal.Record(origin, decision, TunnelLifecycleOutcome.Started);
        try
        {
            await connect();
            cancellationToken.ThrowIfCancellationRequested();
            lifecycleJournal.Record(origin, decision, TunnelLifecycleOutcome.Connected, transport: transportExecution.Current);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            lifecycleJournal.Record(origin, decision, TunnelLifecycleOutcome.Cancelled, "OPERATION_INTERRUPTED");
            throw;
        }
        catch (Exception error)
        {
            lifecycleJournal.Record(origin, decision, TunnelLifecycleOutcome.Failed,
                error is ClientFault fault ? fault.Code : "SERVICE_FAILURE", transportExecution.Current);
            throw;
        }
    }
}
