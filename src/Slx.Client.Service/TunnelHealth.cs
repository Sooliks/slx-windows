using System.Diagnostics;
using Slx.Client.Core;
namespace Slx.Client.Service;
internal sealed partial class TunnelEngine
{
    private readonly RemoteHealthGrace remoteHealthGrace = new();
    public async Task CheckHealthAsync(CancellationToken cancellationToken)
    {
        await lifecycle.WaitAsync(cancellationToken);
        try
        {
            using var work = connectionWork.Begin(cancellationToken);
            CancellationToken serviceToken = cancellationToken;
            cancellationToken = work.Token;
            if(cancellationToken.IsCancellationRequested){serviceToken.ThrowIfCancellationRequested();return;}
            if(await ContinueOffCleanupAsync())return;
            if(TryStartupOffCleanup())return;
            if(!desiredConnected||currentPlan==null)return;
            if(Stopwatch.GetTimestamp()<retryAt)return;
            try
            {
                InteractiveSessions.RequireSingleOwner(ownerSid??"");
                if (!PhysicalPathReady()) return;
                bool wasConnected = state.Phase is TunnelPhase.Connected or TunnelPhase.Checking;
                if(wasConnected && coreRecovery.RequiredFullReason is null && FrontAlive && CoreAlive && ownedAdapterId is not null && LocalProbe.TunnelReady(ownedAdapterId))
                {
                    if(state.Phase==TunnelPhase.Connected && pendingApplicationUpdate is not null && Stopwatch.GetTimestamp()>=applicationReconcileAt)
                    {
                        await ReconcileApplicationsAsync();
                        if(state.Phase!=TunnelPhase.Connected || !state.ProbePassed)return;
                    }
                    if(Stopwatch.GetTimestamp()<healthAt)return;
                    healthAt=Stopwatch.GetTimestamp()+15*Stopwatch.Frequency;
                    var result = await ConnectionHealthCheck.CheckAsync(state,
                        (target, token) => LocalProbe.ObserveAsync(probe.Port, probe.User, probe.Password, target, token),
                        () => HealthResourcesReady(cancellationToken), observed => state = observed, cancellationToken);
                    RecordHealthIncident(result.Confirmation, result.Retained);
                    if(result.Retained)return;
                    await CheckFrontControlAsync(cancellationToken);
                    // A remote-only failure counts against the current transport; lost local resources do not.
                    if(!result.Confirmation.Passed){transportExecution.Failed();rotation.Failed("HEALTH_PROBE_FAILED");PublishAttempt();}
                }
                if(wasConnected)
                {
                    var cause = !FrontAlive || !CoreAlive ? RecoveryCause.CoreExited
                        : ownedAdapterId is not null && LocalProbe.TunnelReady(ownedAdapterId)
                            ? RecoveryCause.ProbeFailed : RecoveryCause.AdapterUnavailable;
                    RecordRecovery(cause);
                    recoveryReason = coreRecovery.RequiredFullReason ?? cause switch
                    {
                        RecoveryCause.CoreExited => TunnelLifecycleReason.CoreExited,
                        RecoveryCause.AdapterUnavailable => TunnelLifecycleReason.AdapterUnavailable,
                        _ => TunnelLifecycleReason.HealthProbeFailed
                    };
                }
                // Select scope before runtime verification: a failed core start/verification must not tear down a healthy TUN.
                var decision = RecoveryDecision(recoveryReason);
                await RunRecoveryAsync(TakeRecoveryOrigin(), decision, cancellationToken);
            }
            catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
            { serviceToken.ThrowIfCancellationRequested(); }
            catch(Exception error)
            {
                string code=error is ClientFault fault?fault.Code:"SERVICE_FAILURE";
                // Failures before scoped work (owner, native policy, route observation) keep the existing fail-closed cleanup.
                var decision = new CoreRecoveryDecision(TunnelLifecycleScope.FullTunnel,
                    coreRecovery.RequiredFullReason ?? CoreRecoveryPolicy.ReasonFor(code));
                var origin = TakeRecoveryOrigin();
                lifecycleJournal.Record(origin, decision, TunnelLifecycleOutcome.Started);
                await FailTunnelRecoveryAsync(code);
                lifecycleJournal.Record(origin, decision, TunnelLifecycleOutcome.Failed, code);
            }
        }
        finally{lifecycle.Release();}
    }

    private bool HealthResourcesReady(CancellationToken cancellationToken)
    {
        if(child is null || child.HasExited || ownedAdapterId is null || !LocalProbe.TunnelReady(ownedAdapterId))return false;
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(TimeSpan.FromSeconds(3));
        try
        {
            var observed = TrafficRouteProbe.Observe(ownedAdapterId, budget.Token);
            cancellationToken.ThrowIfCancellationRequested();
            RecordTrafficRoutes(observed);
            TrafficRouteProbe.RequireReady(observed);
            return true;
        }
        catch(OperationCanceledException) when(!cancellationToken.IsCancellationRequested)
        { throw TrafficRouteFailure(cancellationToken); }
        catch(ClientFault fault) when(fault.Code == TrafficRouteProbe.FailureCode)
        { throw TrafficRouteFailure(cancellationToken); }
    }

    private ClientFault TrafficRouteFailure(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        coreRecovery.RequireFull(TunnelLifecycleReason.TrafficRouteFailed);
        // Invalidate the success observation before awaiting owned process/adapter cleanup.
        RecordRecovery(RecoveryCause.ProbeFailed);
        state=state with{Phase=TunnelPhase.Reconnecting,ObservedAt=DateTimeOffset.UtcNow,
            ProbePassed=false,Code=TrafficRouteProbe.FailureCode,ProtectionActive=protectionActive};
        return new(TrafficRouteProbe.FailureCode);
    }
}
