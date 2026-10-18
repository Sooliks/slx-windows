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

    public static object[] Build(ConnectPlan plan)
    {
        var rules = new List<object>
        {
            new { type = "field", ip = LocalNetworks, outboundTag = "direct" },
            // A real readiness probe must use VPN even though it originates in our service.
            new { type = "field", inboundTag = new[] { "probe" }, outboundTag = "vpn" },
            new { type = "field", process = new[] { "self/" }, outboundTag = "direct" }
        };
        if (IPAddress.TryParse(plan.Profile.Host, out var address))
            rules.Add(new { type = "field", ip = new[] { address.ToString() }, outboundTag = "direct" });
        else
            rules.Add(new { type = "field", domain = new[] { "full:" + plan.Profile.Host }, outboundTag = "direct" });
        if (plan.Mode == RoutingMode.SelectedApplications)
        {
            // Windows DNS Client is shared, so DNS cannot truthfully be attributed to the selected app.
            rules.Add(new { type = "field", port = "53", outboundTag = "direct" });
            rules.Add(new
            {
                type = "field",
                process = plan.Applications.Where(app => app.Enabled).Select(app => app.ExecutablePath.Replace('\\', '/')).ToArray(),
                outboundTag = "vpn"
            });
        }
        rules.Add(new
        {
            type = "field", network = "tcp,udp",
            outboundTag = plan.Mode == RoutingMode.WholeComputer ? "vpn" : "direct"
        });
        return rules.ToArray();
    }
}
