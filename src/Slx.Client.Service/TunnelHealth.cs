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
            bool coreOnly=false;
            try
            {
                InteractiveSessions.RequireSingleOwner(ownerSid??"");
                bool wasConnected = state.Phase is TunnelPhase.Connected or TunnelPhase.Checking;
                if(wasConnected && FrontAlive && CoreAlive && LocalProbe.TunnelReady())
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
                    // A remote-only failure counts against the current transport; lost local resources do not.
                    if(!result.Confirmation.Passed){rotation.Failed("HEALTH_PROBE_FAILED");PublishAttempt();}
                }
                if(wasConnected)
                    RecordRecovery(!FrontAlive||!CoreAlive?RecoveryCause.CoreExited
                        :LocalProbe.TunnelReady()?RecoveryCause.ProbeFailed:RecoveryCause.AdapterUnavailable);
                string executable=RuntimeTrust.Verify();
                if(CanRecoverCoreOnly())
                {
                    // Unselected applications keep their direct flows: only the VPN core is replaced.
                    coreOnly=true;
                    await RecoverCoreAsync(executable,cancellationToken);
                    retryAt=0;
                    return;
                }
                PrepareExactApplicationPaths(cancellationToken);
                state=state with{Phase=TunnelPhase.Reconnecting,ObservedAt=DateTimeOffset.UtcNow,ProbePassed=false,ProtectionActive=protectionActive};
                await StopChildAsync();
                ArmProtection(currentPlan);
                var pinned=pinnedPlan??await TunnelEndpoints.PinAsync(currentPlan,cancellationToken);
                intents.Save(new(ownerSid!,pinned,currentPlan.Profile.Host));
                await LaunchAsync(pinned,executable,TunnelPhase.Reconnecting,cancellationToken);
                retryAt=0;
            }
            catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
            { serviceToken.ThrowIfCancellationRequested(); }
            catch(Exception error)
            {
                string code=error is ClientFault fault?fault.Code:"SERVICE_FAILURE";
                if(coreOnly){await FailCoreRecoveryAsync(code);return;}
                await BackgroundCleanup.TryAsync(StopChildAsync);
                if(code=="VPN_PROBE_FAILED"){rotation.Failed(code);PublishAttempt();}
                ScheduleRecovery(code);
            }
        }
        finally{lifecycle.Release();}
    }

    private bool HealthResourcesReady(CancellationToken cancellationToken)
    {
        if(child is null || child.HasExited || !LocalProbe.TunnelReady() || ownedAdapterId is null)return false;
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
        // Invalidate the success observation before awaiting owned process/adapter cleanup.
        RecordRecovery(RecoveryCause.ProbeFailed);
        state=state with{Phase=TunnelPhase.Reconnecting,ObservedAt=DateTimeOffset.UtcNow,
            ProbePassed=false,Code=TrafficRouteProbe.FailureCode,ProtectionActive=protectionActive};
        return new(TrafficRouteProbe.FailureCode);
    }
}
