using System.Text.Json;
using Slx.Client.Core;
using Xunit;
namespace Slx.Client.Tests;
public sealed class CoreSniffingTests
{
    [Theory]
    [InlineData(RoutingMode.SelectedApplications)]
    [InlineData(RoutingMode.WholeComputer)]
    public void RestoreTlsHostForIpv6WithoutChangingIpv4OrLocalDestinations(RoutingMode mode)
    {
        using var config=JsonDocument.Parse(XrayConfigBuilder.Build(CoreFixtures.Plan(mode),18899,"probeUser00000000","probePassword0000"));
        var inbound=config.RootElement.GetProperty("inbounds")[0];
        var sniff=inbound.GetProperty("sniffing");
        Assert.True(sniff.GetProperty("enabled").GetBoolean());
        Assert.False(sniff.GetProperty("routeOnly").GetBoolean());
        Assert.Equal(["tls"],sniff.GetProperty("destOverride").EnumerateArray().Select(x=>x.GetString()!).ToArray());
        var excluded=sniff.GetProperty("ipsExcluded").EnumerateArray().Select(x=>x.GetString()!).ToArray();
        Assert.Contains("0.0.0.0/0",excluded);
        Assert.Contains("fc00::/7",excluded);
        Assert.Contains("fe80::/10",excluded);
        Assert.Contains("::1/128",excluded);
        Assert.Contains("full:cloudflare-ech.com",sniff.GetProperty("domainsExcluded").EnumerateArray().Select(x=>x.GetString()));
        Assert.False(config.RootElement.GetProperty("inbounds")[1].TryGetProperty("sniffing",out _));
    }
}
