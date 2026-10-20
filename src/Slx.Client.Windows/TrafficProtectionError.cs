using Slx.Client.Core;
namespace Slx.Client.Windows;
internal enum TrafficOperation { Open, Provider, Sublayer, Begin, Commit, Enumerate, Delete, FilterV4, FilterV6, Application, Adapter }
/// <summary>Only an owned operation name and numeric Windows code leave the privileged boundary.</summary>
internal static class TrafficProtectionError
{
    public static void Check(uint code, TrafficOperation operation)
    {
        if (code != 0) throw new ClientFault($"TRAFFIC_PROTECTION_{operation.ToString().ToUpperInvariant()}_{code:X8}");
    }
    public static void Application(uint code, bool selected)
    {
        if (selected && code is 2 or 3) throw new ClientFault("TRAFFIC_APPLICATION_MISSING");
        if (selected && code == 5) throw new ClientFault("TRAFFIC_APPLICATION_DENIED");
        Check(code, TrafficOperation.Application);
    }
}
