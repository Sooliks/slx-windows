using System.Diagnostics;
using Slx.Client.Core;

namespace Slx.Client.Service;

internal sealed partial class TunnelEngine
{
    private async Task HandleExitAsync(Process exited)
    {
        await lifecycle.WaitAsync();
        try
        {
            if (!ReferenceEquals(child, exited)) return;
            string? name = state.ProfileName;
            RoutingMode? mode = state.Mode;
            await StopChildAsync();
            state = new(TunnelPhase.Faulted, DateTimeOffset.UtcNow, name, "CORE_EXITED", Mode: mode);
        }
        finally { lifecycle.Release(); }
    }
}
