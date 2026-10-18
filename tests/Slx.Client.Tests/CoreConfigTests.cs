using System.Text.Json;
using Slx.Client.Core;
using Xunit;

namespace Slx.Client.Tests;

public sealed class CoreConfigTests
{
    private static JsonDocument Build(ConnectPlan plan) =>
        JsonDocument.Parse(XrayConfigBuilder.Build(plan, 18899, "probeUser00000000", "probePassword0000"));

    [Fact]
    public void SelectedModeUsesExactEnabledPathsAndDirectUnmatchedTraffic()
    {
        var plan = CoreFixtures.Plan() with { Applications = [CoreFixtures.App(), CoreFixtures.App(@"C:\Apps\Off.exe", false)] };
        using var config = Build(plan);
        var root = config.RootElement;
        var rules = root.GetProperty("routing").GetProperty("rules").EnumerateArray().ToArray();
        var selected = rules.Single(rule => rule.TryGetProperty("process", out var process)
            && process[0].GetString() != "self/");
        Assert.Equal("C:/Apps/Browser/browser.exe", selected.GetProperty("process")[0].GetString());
        Assert.Single(selected.GetProperty("process").EnumerateArray());
        Assert.Equal("vpn", selected.GetProperty("outboundTag").GetString());
        Assert.Equal("direct", rules[^1].GetProperty("outboundTag").GetString());
        Assert.True(Array.FindIndex(rules, rule => rule.TryGetProperty("inboundTag", out _))
            < Array.FindIndex(rules, rule => rule.TryGetProperty("process", out _)));
        Assert.Equal("direct", rules.Single(rule => rule.TryGetProperty("port", out _)).GetProperty("outboundTag").GetString());
    }

    [Fact]
    public void WholeModeRoutesPublicTrafficAndDnsToVpnWhilePreservingLanAndSelfBypass()
    {
        using var config = Build(CoreFixtures.Plan(RoutingMode.WholeComputer));
        var rules = config.RootElement.GetProperty("routing").GetProperty("rules").EnumerateArray().ToArray();
        Assert.Equal("direct", rules[0].GetProperty("outboundTag").GetString());
        Assert.Contains(rules[0].GetProperty("ip").EnumerateArray(), ip => ip.GetString() == "fc00::/7");
        Assert.Contains(rules, rule => rule.TryGetProperty("process", out var process) && process[0].GetString() == "self/");
        Assert.DoesNotContain(rules, rule => rule.TryGetProperty("port", out _));
        Assert.Equal("vpn", rules[^1].GetProperty("outboundTag").GetString());
    }

    [Fact]
    public void TunIsDualStackAndProbeIsAuthenticatedLocalhostOnly()
    {
        using var config = Build(CoreFixtures.Plan());
        var inbounds = config.RootElement.GetProperty("inbounds");
        var tun = inbounds[0].GetProperty("settings");
        Assert.Equal("SLX VPN", tun.GetProperty("name").GetString());
        Assert.Equal(["0.0.0.0/0", "::/0"], tun.GetProperty("autoSystemRoutingTable").EnumerateArray().Select(item => item.GetString()!).ToArray());
        Assert.Equal("auto", tun.GetProperty("autoOutboundsInterface").GetString());
        Assert.Equal("127.0.0.1", inbounds[1].GetProperty("listen").GetString());
        Assert.Equal("probePassword0000", inbounds[1].GetProperty("settings").GetProperty("accounts")[0].GetProperty("pass").GetString());
        Assert.Equal("none", config.RootElement.GetProperty("log").GetProperty("loglevel").GetString());
    }

    [Theory]
    [InlineData(CoreFixtures.Hysteria, "hysteria", "h3")]
    [InlineData(CoreFixtures.Xhttp, "xhttp", "h2")]
    public void SecureProfilesCannotDisableTlsValidation(string uri, string network, string alpn)
    {
        using var config = Build(CoreFixtures.Plan() with { Profile = CoreFixtures.Profile(uri) });
        var stream = config.RootElement.GetProperty("outbounds")[1].GetProperty("streamSettings");
        Assert.Equal(network, stream.GetProperty("network").GetString());
        Assert.False(stream.GetProperty("tlsSettings").GetProperty("allowInsecure").GetBoolean());
        Assert.Equal(alpn, stream.GetProperty("tlsSettings").GetProperty("alpn")[0].GetString());
        if (network == "xhttp") Assert.Equal("unsafe", stream.GetProperty("tlsSettings").GetProperty("fingerprint").GetString());
        else Assert.False(stream.GetProperty("tlsSettings").TryGetProperty("fingerprint", out _));
    }

    [Fact]
    public void LegacyRealityPreservesIdentityAndNeverEnablesGlobalMux()
    {
        using var config = Build(CoreFixtures.Plan() with { Profile = CoreFixtures.Profile(CoreFixtures.Reality) });
        var outbound = config.RootElement.GetProperty("outbounds")[1];
        Assert.Equal(CoreFixtures.Token, outbound.GetProperty("settings").GetProperty("vnext")[0].GetProperty("users")[0].GetProperty("id").GetString());
        Assert.Equal("reality", outbound.GetProperty("streamSettings").GetProperty("security").GetString());
        Assert.False(outbound.TryGetProperty("mux", out _));
    }
}
