using System.Text;
using Slx.Client.Core;
namespace Slx.Client.Desktop.Presentation;
internal static class TrafficRouteDiagnostics
{
    public static void Append(StringBuilder text, TrafficRouteCheckObservation? routes)
    {
        if (routes is null) return;
        text.AppendLine($"Traffic route check UTC: {routes.At:O}");
        AppendRoute(text, "IPv4 lower", routes.Ipv4Low);
        AppendRoute(text, "IPv4 upper", routes.Ipv4High);
        if (routes.Ipv6 is {} ipv6) AppendRoute(text, "IPv6", ipv6);
        else text.AppendLine("Traffic route IPv6: not checked (no usable public IPv6 source)");
        text.AppendLine("Traffic route meaning: representative ordinary-route observations, not a packet or every-destination test");
    }
    private static void AppendRoute(StringBuilder text, string label, DnsRouteObservation route)
    {
        text.Append($"Traffic route {label}: {route.Status}");
        if (route.NativeError is {} error) text.Append($", native_error={error}");
        if (route.PrefixLength is {} prefix) text.Append($", prefix_length={prefix}");
        if (route.RouteMetric is {} metric) text.Append($", route_metric={metric}");
        text.AppendLine();
    }
}
