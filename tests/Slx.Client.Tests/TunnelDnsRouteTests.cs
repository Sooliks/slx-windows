using System.Net;
using System.Text.Json;
using Slx.Client.Core;
using Slx.Client.Windows;
using Xunit;

namespace Slx.Client.Tests;

public sealed class TunnelDnsRouteTests
{
    private static readonly Guid Tunnel = new("c5656347-93ca-8b73-7336-a92e8199b47b");
    private static readonly Guid Other = new("9fb8ed64-e603-4642-b60a-7ea3a1515b60");
    private static readonly IPAddress Destination = IPAddress.Parse("1.1.1.1");

    [Fact]
    public void OnlyCurrentOwnedInterfaceCanPassTheRouteObservation()
    {
        var result = TunnelDnsRoute.Observe(Tunnel.ToString("B").ToUpperInvariant(), Destination,
            destination => { Assert.Equal(Destination, destination); return (0, 42); },
            luid => { Assert.Equal(42UL, luid); return (0, Tunnel); });
        Assert.Equal(new DnsRouteObservation(DnsRouteStatus.OwnedTunnel), result);
        Assert.Equal(new DnsRouteObservation(DnsRouteStatus.OtherInterface), Observe(identity: Other));
    }

    [Theory]
    [InlineData(5U)]
    [InlineData(50U)]
    [InlineData(87U)]
    [InlineData(1168U)]
    public void RouteLookupFailureCannotUsePartialNativeOutput(uint error)
    {
        var result = TunnelDnsRoute.Observe(Tunnel.ToString(), Destination, _ => (error, 42),
            _ => throw new InvalidOperationException("An unsuccessful route must not be resolved."));
        Assert.Equal(new DnsRouteObservation(DnsRouteStatus.Unavailable, error), result);
    }

    [Fact]
    public void MissingNativeIdentityIsUnavailableRatherThanAMatch()
    {
        var result = TunnelDnsRoute.Observe(Tunnel.ToString(), Destination, _ => (0, 0),
            _ => throw new InvalidOperationException("A missing LUID must not be resolved."));
        Assert.Equal(new DnsRouteObservation(DnsRouteStatus.Unavailable), result);
        Assert.Equal(new DnsRouteObservation(DnsRouteStatus.Unavailable), Observe(identity: Guid.Empty));
        Assert.Equal(new DnsRouteObservation(DnsRouteStatus.Unavailable, 87), Observe(identityError: 87));
    }

    [Theory]
    [InlineData("")]
    [InlineData("SLX VPN")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("9fb8ed64-e603-4642-b60a-7ea3a1515b60")]
    public void InvalidOrForeignOwnedIdentityCannotTriggerNativeLookup(string owned)
        => AssertRejectedBeforeLookup(owned, Destination);

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("172.30.255.1")]
    [InlineData("0.0.0.0")]
    [InlineData("2606:4700:4700::1111")]
    [InlineData("::ffff:1.1.1.1")]
    public void UnsupportedOrNonpublicDestinationCannotBeUsedAsDnsReadiness(string destination)
        => AssertRejectedBeforeLookup(Tunnel.ToString(), IPAddress.Parse(destination));

    [Fact]
    public void RepeatedObservationsDoNotCacheTheOldInterface()
    {
        var identities = new Queue<Guid>([Tunnel, Other]);
        DnsRouteObservation Read() => TunnelDnsRoute.Observe(Tunnel.ToString(), Destination,
            _ => (0, 42), _ => (0, identities.Dequeue()));
        Assert.Equal(DnsRouteStatus.OwnedTunnel, Read().Status);
        Assert.Equal(DnsRouteStatus.OtherInterface, Read().Status);
        Assert.Empty(identities);
    }

    [Fact]
    public void DiagnosticsContainNoRouteAddressOrInterfaceIdentity()
    {
        var result = Observe(identity: Other);
        string json = JsonSerializer.Serialize(result);
        Assert.DoesNotContain(Other.ToString(), json);
        Assert.DoesNotContain(Tunnel.ToString(), json);
        Assert.DoesNotContain(Destination.ToString(), json);
        using var document = JsonDocument.Parse(json);
        Assert.Equal(new[] { "Status", "NativeError" },
            document.RootElement.EnumerateObject().Select(property => property.Name).ToArray());
    }

    private static DnsRouteObservation Observe(Guid? identity = null, uint identityError = 0)
        => TunnelDnsRoute.Observe(Tunnel.ToString(), Destination, _ => (0, 42),
            _ => (identityError, identity ?? Tunnel));

    private static void AssertRejectedBeforeLookup(string owned, IPAddress destination)
    {
        var result = TunnelDnsRoute.Observe(owned, destination,
            _ => throw new InvalidOperationException("Invalid input must not inspect routes."),
            _ => throw new InvalidOperationException("Invalid input must not inspect adapters."));
        Assert.Equal(new DnsRouteObservation(DnsRouteStatus.Unavailable), result);
    }
}
