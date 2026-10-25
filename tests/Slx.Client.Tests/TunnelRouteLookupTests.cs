using System.Net;
using Slx.Client.Core;
using Slx.Client.Windows;
using Xunit;

namespace Slx.Client.Tests;

public sealed class TunnelRouteLookupTests
{
    private static readonly Guid Tunnel = new("c5656347-93ca-8b73-7336-a92e8199b47b");

    [Theory]
    [InlineData("8.8.8.8", 1)]
    [InlineData("142.250.0.1", 32)]
    [InlineData("2001:4860:4860::8888", 1)]
    [InlineData("2001:4860:4860::8888", 128)]
    public void DataRouteReadsValidateBothNativeAddressFamilies(string address, byte prefix)
    {
        var destination = IPAddress.Parse(address);
        var observed = TunnelRouteLookup.Observe(Tunnel.ToString(), destination, actual =>
        { Assert.Equal(destination, actual); return (0, 42, prefix, 0); }, _ => (0, Tunnel));
        Assert.Equal(new DnsRouteObservation(DnsRouteStatus.OwnedTunnel, PrefixLength: prefix, RouteMetric: 0), observed);
    }

    [Theory]
    [InlineData("8.8.8.8", 33)]
    [InlineData("2001:4860:4860::8888", 129)]
    public void InvalidPrefixCannotProduceOwnedRouteSuccess(string address, byte prefix)
    {
        var result = TunnelRouteLookup.Observe(Tunnel.ToString(), IPAddress.Parse(address),
            _ => (0, 42, prefix, 0), _ => throw new InvalidOperationException("Invalid row cannot resolve identity."));
        Assert.Equal(DnsRouteStatus.Unavailable, result.Status);
    }

    [Fact]
    public void ForeignIpv6RouteCannotPassEvenWithZeroMetric()
    {
        var result = TunnelRouteLookup.Observe(Tunnel.ToString(), IPAddress.Parse("2001:4860:4860::8888"),
            _ => (0, 42, 64, 0), _ => (0, Guid.NewGuid()));
        Assert.Equal(DnsRouteStatus.OtherInterface, result.Status);
        Assert.Equal((byte)64, result.PrefixLength);
    }
}
