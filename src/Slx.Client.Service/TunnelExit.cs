using System.Diagnostics;
namespace Slx.Client.Service;
internal sealed partial class TunnelEngine
{
    private async Task HandleExitAsync(Process exited)
    {
        await lifecycle.WaitAsync();
        try
        {
            if(!ReferenceEquals(child,exited))return;
            if(state.Phase is Slx.Client.Core.TunnelPhase.Connected or Slx.Client.Core.TunnelPhase.Checking)
                RecordRecovery(Slx.Client.Core.RecoveryCause.CoreExited);
            if (desiredConnected) lifecycleJournal.Record(Slx.Client.Core.TunnelLifecycleOrigin.AutomaticRecovery,
                new(Slx.Client.Core.TunnelLifecycleScope.FullTunnel, Slx.Client.Core.TunnelLifecycleReason.FrontUnavailable),
                Slx.Client.Core.TunnelLifecycleOutcome.Failed, "CORE_EXITED");
            await BackgroundCleanup.TryAsync(StopChildAsync);
            if(desiredConnected)
            {
                ScheduleRecovery("CORE_EXITED");
                recoveryReason = Slx.Client.Core.TunnelLifecycleReason.FrontUnavailable;
            }
        }
        finally{lifecycle.Release();}
    }
}
