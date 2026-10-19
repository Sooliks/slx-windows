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
            if(!desiredConnected||currentPlan==null)return;
            if(Stopwatch.GetTimestamp()<retryAt)return;
            try
            {
                InteractiveSessions.RequireSingleOwner(ownerSid??"");
                bool wasConnected = state.Phase == TunnelPhase.Connected;
                if(wasConnected && child is not null && !child.HasExited && LocalProbe.TunnelReady())
                {
                    if(Stopwatch.GetTimestamp()<healthAt)return;
                    healthAt=Stopwatch.GetTimestamp()+15*Stopwatch.Frequency;
                    var result = await HealthConfirmation.CheckAsync(
                        (target, token) => LocalProbe.ObserveAsync(probe.Port, probe.User, probe.Password, target, token),
                        () => state = state with { Phase=TunnelPhase.Reconnecting,
                            ObservedAt=DateTimeOffset.UtcNow, ProbePassed=false }, cancellationToken);
                    bool retained = result.Passed && !child.HasExited && LocalProbe.TunnelReady();
                    RecordHealthIncident(result, retained);
                    if(retained)
                    {
                        state=state with { Phase=TunnelPhase.Connected, ObservedAt=DateTimeOffset.UtcNow, ProbePassed=true };
                        return;
                    }
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
            catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested){throw;}
            catch(Exception error)
            {
                await BackgroundCleanup.TryAsync(StopChildAsync);
                ScheduleRecovery(error is ClientFault fault?fault.Code:"SERVICE_FAILURE");
            }
        }
        finally{lifecycle.Release();}
    }
}
