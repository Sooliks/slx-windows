using System.Text.Json;

namespace Slx.Client.Core;

/// <summary>
/// The VPN core owns only the remote transport. It has no TUN, routes or DNS, so restarting it or switching
/// transport never touches traffic of unselected applications, which the TUN process sends directly.
/// </summary>
public static class XrayCoreConfig
{
    public static string Build(VpnProfile profile, XrayCoreLink link)
    {
        PlanValidator.ValidateProfile(profile);
        ArgumentNullException.ThrowIfNull(link);
        link.Validate();
        var config = new
        {
            log = new { access = "none", error = "none", loglevel = "none" },
            inbounds = new object[]
            {
                new
                {
                    tag = "front", listen = "127.0.0.1", port = link.Port, protocol = "vless",
                    settings = new { clients = new[] { new { id = link.Id } }, decryption = "none" },
                    streamSettings = new { network = "raw", security = "none" }
                }
            },
            outbounds = new object[]
            {
                XrayOutbound.Build(profile),
                new { tag = "block", protocol = "blackhole", settings = new { } }
            },
            routing = new
            {
                domainStrategy = "AsIs",
                rules = new object[] { new { type = "field", inboundTag = new[] { "front" }, outboundTag = "vpn" } }
            }
        };
        return JsonSerializer.Serialize(config);
    }
}
