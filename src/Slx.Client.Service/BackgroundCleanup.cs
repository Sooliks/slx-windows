using Slx.Client.Core;

namespace Slx.Client.Service;

internal static class BackgroundCleanup
{
    public static bool IsPending(ClientFault fault) => fault.Code is "TUN_CLEANUP_PENDING" or "CORE_STOP_FAILED";

    public static async Task<bool> TryAsync(Func<Task> cleanup)
    {
        try { await cleanup(); return true; }
        catch (ClientFault fault) when (IsPending(fault)) { return false; }
    }
}
