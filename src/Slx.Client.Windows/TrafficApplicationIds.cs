using Slx.Client.Core;
using Windows.Win32;
using Windows.Win32.NetworkManagement.WindowsFilteringPlatform;
namespace Slx.Client.Windows;
internal sealed record TrafficApplicationId(byte[] Identity, bool Permit)
{
    public override string ToString() => "TrafficApplicationId [redacted]";
}
internal static class TrafficApplicationIds
{
    public static IReadOnlyList<TrafficApplicationId> Resolve(ConnectPlan plan, string corePath, string servicePath,
        Func<string, bool, byte[]>? resolve = null)
    {
        resolve ??= Read;
        // A removed Discord/Roblox version is a rejected plan, never a silently unprotected application.
        // Resolve the complete set before opening the replacement transaction.
        return plan.Mode == RoutingMode.WholeComputer
            ? [new(resolve(corePath, false), true), new(resolve(servicePath, false), true)]
            : plan.Applications.Where(app => app.Enabled)
                .Select(app => new TrafficApplicationId(resolve(app.ExecutablePath, true), false)).ToArray();
    }
    internal static unsafe byte[] Read(string path, bool selected)
    {
        FWP_BYTE_BLOB* application = null;
        uint code = PInvoke.FwpmGetAppIdFromFileName0(path, out application);
        try
        {
            TrafficProtectionError.Application(code, selected);
            if (application is null || application->data is null || application->size is 0 or > 65536)
                throw new ClientFault("TRAFFIC_PROTECTION_APPLICATION_INVALID");
            return new ReadOnlySpan<byte>(application->data, checked((int)application->size)).ToArray();
        }
        finally { if (application is not null) PInvoke.FwpmFreeMemory0((void**)&application); }
    }
}
