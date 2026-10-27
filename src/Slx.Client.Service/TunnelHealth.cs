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
                bool wasConnected = state.Phase == TunnelPhase.Connected || state.Phase == TunnelPhase.Checking && remoteHealthGrace.Pending;
                if(wasConnected && child is not null && !child.HasExited && LocalProbe.TunnelReady()
                    && (!remoteHealthGrace.Pending || remoteHealthGrace.Remaining > TimeSpan.Zero && HealthResourcesReady(cancellationToken)))
                {
                    if(state.Phase==TunnelPhase.Connected && pendingApplicationUpdate is not null && Stopwatch.GetTimestamp()>=applicationReconcileAt)
                    {
                        await ReconcileApplicationsAsync();
                        if(state.Phase!=TunnelPhase.Connected || !state.ProbePassed)return;
                    }
                    if(Stopwatch.GetTimestamp()<healthAt)return;
                    healthAt=Stopwatch.GetTimestamp()+15*Stopwatch.Frequency;
                    using var checkBudget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    if(remoteHealthGrace.Pending)checkBudget.CancelAfter(remoteHealthGrace.Remaining);
                    ConnectionHealthResult? result=null;
                    try
                    {
                        result = await ConnectionHealthCheck.CheckAsync(state,
                            (target, token) => LocalProbe.ObserveAsync(probe.Port, probe.User, probe.Password, target, token),
                            () => HealthResourcesReady(checkBudget.Token), observed => state = observed, checkBudget.Token,
                            deferFailure: currentPlan.Mode==RoutingMode.SelectedApplications?()=>remoteHealthGrace.Defer(currentPlan.Mode):null);
                    }
                    catch(OperationCanceledException) when(!cancellationToken.IsCancellationRequested && remoteHealthGrace.Pending
                        && remoteHealthGrace.Remaining<=TimeSpan.Zero) { }
                    if(result is not null)
                    {
                        RecordHealthIncident(result.Confirmation, result.Retained);
                        if(result.Retained){remoteHealthGrace.Reset();return;}
                        if(result.Deferred){healthAt=Stopwatch.GetTimestamp()+5*Stopwatch.Frequency;return;}
                    }
                }
                if(wasConnected)
                    RecordRecovery(child is null||child.HasExited?RecoveryCause.CoreExited
                        :LocalProbe.TunnelReady()?RecoveryCause.ProbeFailed:RecoveryCause.AdapterUnavailable);
                PrepareExactApplicationPaths(cancellationToken);
                state=state with{Phase=TunnelPhase.Reconnecting,ObservedAt=DateTimeOffset.UtcNow,ProbePassed=false,ProtectionActive=protectionActive};
                await StopChildAsync();
                ArmProtection(currentPlan);
                var pinned=pinnedPlan??await PinEndpointAsync(currentPlan,cancellationToken);
                intents.Save(new(ownerSid!,pinned,currentPlan.Profile.Host));
                await LaunchAsync(pinned,RuntimeTrust.Verify(),TunnelPhase.Reconnecting,cancellationToken);
                retryAt=0;
            }
            catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
            { serviceToken.ThrowIfCancellationRequested(); }
            catch(Exception error)
            {
                await BackgroundCleanup.TryAsync(StopChildAsync);
                ScheduleRecovery(error is ClientFault fault?fault.Code:"SERVICE_FAILURE");
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
