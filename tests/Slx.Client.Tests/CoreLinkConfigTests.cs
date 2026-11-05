using System.Text.Json;
using Slx.Client.Core;
using Slx.Client.Windows;
using Xunit;

namespace Slx.Client.Tests;

public sealed class CoreLinkConfigTests
{
    private const string Core = @"C:\Program Files\SLX VPN\service\runtime\xray.exe";
    private static readonly XrayCoreLink Link = new(28899, "linkUser000000000", "linkPassword00000", Core);

    [Fact]
    public void VpnCoreHasOnlyAnAuthenticatedLoopbackInboundAndTheRemoteTransport()
    {
        using var config = JsonDocument.Parse(XrayCoreConfig.Build(CoreFixtures.Profile(), Link));
        var root = config.RootElement;
        Assert.False(root.TryGetProperty("dns", out _));
        Assert.False(root.TryGetProperty("api", out _));
        var inbound = Assert.Single(root.GetProperty("inbounds").EnumerateArray());
        Assert.Equal("socks", inbound.GetProperty("protocol").GetString());
        Assert.Equal("127.0.0.1", inbound.GetProperty("listen").GetString());
        Assert.Equal(28899, inbound.GetProperty("port").GetInt32());
        var settings = inbound.GetProperty("settings");
        Assert.Equal("password", settings.GetProperty("auth").GetString());
        Assert.True(settings.GetProperty("udp").GetBoolean());
        Assert.Equal("linkUser000000000", settings.GetProperty("accounts")[0].GetProperty("user").GetString());
        var outbounds = root.GetProperty("outbounds").EnumerateArray().ToArray();
        Assert.Equal("vpn", outbounds[0].GetProperty("tag").GetString());
        Assert.Equal("hysteria", outbounds[0].GetProperty("protocol").GetString());
        Assert.DoesNotContain(outbounds, outbound => outbound.GetProperty("protocol").GetString() == "freedom");
    }

    [Fact]
    public void TunProcessSendsVpnTrafficThroughTheLinkAndKeepsTheCoreDirect()
    {
        var reality = CoreFixtures.Profile(CoreFixtures.Reality) with { Host = "93.184.216.35" };
        var plan = CoreFixtures.Plan() with { Profile = CoreFixtures.Profile() with { Host = "93.184.216.34" }, Alternates = [reality] };
        using var config = JsonDocument.Parse(XrayConfigBuilder.Build(plan, 18899, "probeUser00000000", "probePassword0000", "auto", Link));
        var root = config.RootElement;
        var vpn = root.GetProperty("outbounds").EnumerateArray().Single(outbound => outbound.GetProperty("tag").GetString() == "vpn");
        Assert.Equal("socks", vpn.GetProperty("protocol").GetString());
        var server = vpn.GetProperty("settings").GetProperty("servers")[0];
        Assert.Equal(("127.0.0.1", 28899), (server.GetProperty("address").GetString(), server.GetProperty("port").GetInt32()));
        var rules = root.GetProperty("routing").GetProperty("rules").EnumerateArray().ToArray();
        int coreRule = Array.FindIndex(rules, rule => rule.TryGetProperty("process", out var process)
            && process[0].GetString() == Core.Replace('\\', '/'));
        int selected = Array.FindIndex(rules, rule => rule.TryGetProperty("process", out var process)
            && process[0].GetString() == "C:/Apps/Browser/browser.exe");
        Assert.InRange(coreRule, 0, selected - 1);
        Assert.Equal("direct", rules[coreRule].GetProperty("outboundTag").GetString());
        foreach (var address in new[] { "93.184.216.34", "93.184.216.35" })
            Assert.Equal("direct", rules.Single(rule => rule.TryGetProperty("ip", out var ips) && ips[0].GetString() == address)
                .GetProperty("outboundTag").GetString());
        Assert.Contains("0.0.0.0/1", root.GetProperty("inbounds")[0].GetProperty("settings").GetRawText(), StringComparison.Ordinal);
        Assert.Equal("direct", rules[^1].GetProperty("outboundTag").GetString());
    }

    [Fact]
    public void InvalidLinkAndCorePathAreRejected()
    {
        Assert.Throws<ClientFault>(() => XrayCoreConfig.Build(CoreFixtures.Profile(), Link with { Port = 80 }));
        Assert.Throws<ClientFault>(() => XrayCoreConfig.Build(CoreFixtures.Profile(), Link with { Password = "short" }));
        Assert.Throws<ClientFault>(() => XrayCoreConfig.Build(CoreFixtures.Profile(), Link with { CorePath = "xray" }));
        Assert.Throws<ClientFault>(() => XrayRouteProtocol.BuildReplacement(CoreFixtures.Plan(), "relative.exe"));
        Assert.Throws<ClientFault>(() => new XrayRouteControl(18899, "probeUser00000000", "probePassword0000", "x"));
        Assert.NotEqual(XrayRouteProtocol.BuildReplacement(CoreFixtures.Plan()),
            XrayRouteProtocol.BuildReplacement(CoreFixtures.Plan(), Core));
        Assert.DoesNotContain("linkPassword", Link.ToString(), StringComparison.Ordinal);
    }
}
