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
            await BackgroundCleanup.TryAsync(StopChildAsync);
            if(desiredConnected)ScheduleRecovery("CORE_EXITED");
        }
        finally{lifecycle.Release();}
    }
}
