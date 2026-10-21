using System.Globalization;
using System.Net;
namespace Slx.Client.Core;
/// <summary>Fixed Xray 26.9.9 RoutingService schema; never accepts arbitrary protobuf/configuration.</summary>
public static class XrayRouteProtocol
{
    public const string Host = "slx-control.invalid";
    public const string ApiTag = "slx-control";
    public const string MarkerPrefix = "slx-route-";
    public const string Service = "xray.app.router.command.RoutingService";
    public static byte[] BuildReplacement(ConnectPlan plan)
    {
        PlanValidator.Validate(plan);
        var config = new RouteProtoWriter();
        foreach (var rule in XrayRouting.Build(plan)) config.Bytes(2, Encode(rule));
        var typed = new RouteProtoWriter().Text(1, "xray.app.router.Config").Bytes(2, config.Finish());
        return new RouteProtoWriter().Bytes(1, typed.Finish()).Number(2, 0).Finish();
    }
    private static byte[] Encode(XrayRouteRule rule)
    {
        var message = new RouteProtoWriter().Text(1, rule.OutboundTag);
        if (rule.RuleTag is { } tag) message.Text(19, tag);
        foreach (var inbound in rule.Inbounds ?? []) message.Text(8, inbound);
        foreach (var process in rule.Processes ?? []) message.Text(21, process);
        foreach (var domain in rule.Domains ?? [])
        {
            var full = new RouteProtoWriter().Number(1, 3).Text(2, domain[5..]);
            message.Bytes(2, new RouteProtoWriter().Bytes(2, full.Finish()).Finish());
        }
        foreach (var value in rule.Ips ?? [])
        {
            var parts = value.Split('/'); var address = IPAddress.Parse(parts[0]);
            var bits = parts.Length == 2 ? uint.Parse(parts[1], CultureInfo.InvariantCulture) : (uint)address.GetAddressBytes().Length * 8;
            var cidr = new RouteProtoWriter().Bytes(1, address.GetAddressBytes()).Number(2, bits);
            var custom = new RouteProtoWriter().Bytes(1, cidr.Finish());
            message.Bytes(10, new RouteProtoWriter().Bytes(2, custom.Finish()).Finish());
        }
        if (rule.Port is { } port)
        {
            uint number = uint.Parse(port, CultureInfo.InvariantCulture);
            var range = new RouteProtoWriter().Number(1, number).Number(2, number);
            message.Bytes(14, new RouteProtoWriter().Bytes(1, range.Finish()).Finish());
        }
        foreach (var network in rule.Network?.Split(',') ?? []) message.Number(13, network == "tcp" ? 2u : 3u);
        return message.Finish();
    }
}
