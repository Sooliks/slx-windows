using System.Diagnostics;
using Slx.Client.Core;
namespace Slx.Client.Service;
internal sealed partial class TunnelEngine
{
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
                bool wasConnected = state.Phase == TunnelPhase.Connected;
                if(wasConnected && child is not null && !child.HasExited && LocalProbe.TunnelReady())
                {
                    if(pendingApplicationUpdate is not null && Stopwatch.GetTimestamp()>=applicationReconcileAt)
                        await ReconcileApplicationsAsync();
                    if(Stopwatch.GetTimestamp()<healthAt)return;
                    healthAt=Stopwatch.GetTimestamp()+15*Stopwatch.Frequency;
                    var result = await ConnectionHealthCheck.CheckAsync(state,
                        (target, token) => LocalProbe.ObserveAsync(probe.Port, probe.User, probe.Password, target, token),
                        () => child is not null && !child.HasExited && LocalProbe.TunnelReady(),
                        observed => state = observed, cancellationToken);
                    RecordHealthIncident(result.Confirmation, result.Retained);
                    if(result.Retained)return;
                }
                if(wasConnected)
                    RecordRecovery(child is null||child.HasExited?RecoveryCause.CoreExited
                        :LocalProbe.TunnelReady()?RecoveryCause.ProbeFailed:RecoveryCause.AdapterUnavailable);
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
}
