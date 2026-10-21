using System.Net;

namespace Slx.Client.Core;

internal static class XrayRouting
{
    private static readonly string[] LocalNetworks =
    [
        "0.0.0.0/8", "10.0.0.0/8", "100.64.0.0/10", "127.0.0.0/8", "169.254.0.0/16",
        "172.16.0.0/12", "192.168.0.0/16", "224.0.0.0/4", "240.0.0.0/4",
        "::/128", "::1/128", "fc00::/7", "fe80::/10", "ff00::/8"
    ];

    public static XrayRouteRule[] Build(ConnectPlan plan)
    {
        var rules = new List<XrayRouteRule>
        {
            new(XrayRouteProtocol.ApiTag) { Inbounds = ["probe"], Domains = ["full:" + XrayRouteProtocol.Host], Network = "tcp", Port = "80" },
            new("direct") { Ips = LocalNetworks },
            // A real readiness probe must use VPN even though it originates in our service.
            new("vpn") { Inbounds = ["probe", "resolver"] },
            new("dns") { Inbounds = ["tun"], Port = "53" },
            new("direct") { Processes = ["self/"] }
        };
        if (IPAddress.TryParse(plan.Profile.Host, out var address))
            rules.Add(new("direct") { Ips = [address.ToString()] });
        else
            rules.Add(new("direct") { Domains = ["full:" + plan.Profile.Host] });
        if (plan.Mode == RoutingMode.SelectedApplications)
        {

            rules.Add(new("vpn") { Processes = plan.Applications.Where(app => app.Enabled).Select(app => app.ExecutablePath.Replace('\\', '/')).ToArray() });
        }
        rules.Add(new(plan.Mode == RoutingMode.WholeComputer ? "vpn" : "direct")
        { Network = "tcp,udp", RuleTag = XrayRouteProtocol.MarkerPrefix + XrayRouteRevision.For(plan) });
        return rules.ToArray();
    }
}
