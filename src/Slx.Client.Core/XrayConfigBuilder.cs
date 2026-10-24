using System.Text.Json;

namespace Slx.Client.Core;

/// <summary>Only typed plans become configuration. Never accepts arbitrary Xray JSON.</summary>
public static class XrayConfigBuilder
{
    public static string Build(ConnectPlan plan, int probePort, string probeUser, string probePassword,
        string outboundInterface = "auto")
    {
        PlanValidator.Validate(plan);
        if (probePort is < 1024 or > 65535 || !SafeProbe(probeUser) || !SafeProbe(probePassword))
            throw new ClientFault("INVALID_PLAN");
        if (string.IsNullOrWhiteSpace(outboundInterface) || outboundInterface.Length > 256
            || outboundInterface.Any(char.IsControl)) throw new ClientFault("INVALID_PLAN");
        var config = new
        {
            api = new { tag = XrayRouteProtocol.ApiTag, services = new[] { "RoutingService" } },
            dns = new { servers = new[] { "https://1.1.1.1/dns-query", "https://8.8.8.8/dns-query" }, tag = "resolver", queryStrategy = "UseIP", disableFallback = true },
            log = new { access = "none", error = "none", loglevel = "none" },
            inbounds = new object[]
            {
                new
                {
                    tag = "tun", protocol = "tun",
                    sniffing = new
                    {
                        enabled = true, destOverride = new[] { "tls" }, routeOnly = false,
                        ipsExcluded = new[] { "0.0.0.0/0", "::/128", "::1/128", "fc00::/7", "fe80::/10", "ff00::/8" },
                        domainsExcluded = new[] { "full:cloudflare-ech.com" }
                    },
                    settings = new
                    {
                        name = "SLX VPN", desc = "SLX VPN", mtu = 1500,
                        gateway = new[] { "172.30.255.1/30", "fd73:6c78::1/126" },
                        dns = new[] { "1.1.1.1", "2606:4700:4700::1111" },
                        autoSystemRoutingTable = new[] { "0.0.0.0/0", "::/0" },
                        autoOutboundsInterface = outboundInterface
                    }
                },
                new
                {
                    tag = "probe", listen = "127.0.0.1", port = probePort, protocol = "http",
                    settings = new
                    {
                        allowTransparent = false,
                        accounts = new[] { new { user = probeUser, pass = probePassword } }
                    }
                }
            },
            outbounds = new object[]
            {
                new { tag = "direct", protocol = "freedom", settings = new { domainStrategy = "AsIs" } },
                XrayOutbound.Build(plan.Profile),
                new { tag = "dns", protocol = "dns", settings = new { } }
            },
            routing = new { domainStrategy = "AsIs", rules = XrayRouting.Build(plan) }
        };
        return JsonSerializer.Serialize(config);
    }

    private static bool SafeProbe(string value) => !string.IsNullOrWhiteSpace(value)
        && value.Length is >= 16 and <= 128 && value.All(character => char.IsAsciiLetterOrDigit(character)
            || character is '-' or '_' or '=' or '+');
}
