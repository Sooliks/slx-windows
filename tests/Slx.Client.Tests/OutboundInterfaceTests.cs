using System.Buffers.Binary;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text.Json;
using Slx.Client.Core;
using Slx.Client.Windows;
using Xunit;

namespace Slx.Client.Tests;

public sealed class OutboundInterfaceTests
{
    private static readonly Guid Ethernet = new("9fb8ed64-e603-4642-b60a-7ea3a1515b60");
    private static readonly Guid Wifi = new("b30c1059-a1ac-4c07-b02b-3c69781a02b7");
    private static OutboundInterface.Candidate[] Adapters() =>
    [
        new(Wifi, "Wi-Fi", true, NetworkInterfaceType.Wireless80211),
        new(Ethernet, "Ethernet", true, NetworkInterfaceType.Ethernet)
    ];

    [Fact]
    public void HonorsWindowsSelectedRouteInsteadOfPreferringAnyWifiAdapter()
    {
        Assert.Equal("Ethernet", OutboundInterface.Select(Ethernet, Adapters()));
        Assert.Equal("Wi-Fi", OutboundInterface.Select(Wifi, Adapters()));
        Assert.Equal("Ethernet", OutboundInterface.Select(Ethernet, Adapters().Reverse()));
    }

    [Fact]
    public void MissingOrDownChosenRouteCannotFallBackToAnotherAdapter()
    {
        AssertUnavailable(() => OutboundInterface.Select(Guid.NewGuid(), Adapters()));
        AssertUnavailable(() => OutboundInterface.Select(Ethernet, [Adapters()[1] with { Up = false }, Adapters()[0]]));
        AssertUnavailable(() => OutboundInterface.Select(Ethernet, [Adapters()[1], Adapters()[1]]));
        AssertUnavailable(() => OutboundInterface.Select(Ethernet, [Adapters()[1] with { Type = NetworkInterfaceType.Loopback }]));
    }

    [Fact]
    public void OwnTunAndReservedCoreAutomaticSelectorAreRejected()
    {
        Assert.Equal("TUN_ADAPTER_CONFLICT", Assert.Throws<ClientFault>(() =>
            OutboundInterface.Select(Ethernet, [Adapters()[1] with { Name = "SLX VPN" }])).Code);
        AssertUnavailable(() => OutboundInterface.Select(Ethernet, [Adapters()[1] with { Name = "auto" }]));
        AssertUnavailable(() => OutboundInterface.Select(Ethernet, [Adapters()[1] with { Name = "Ethernet\n" }]));
    }

    [Fact]
    public void OwnTunIdentityCannotBecomeTheOutboundInterfaceAfterRename()
    {
        var identity = new Guid("c5656347-93ca-8b73-7336-a92e8199b47b");
        Assert.Equal("TUN_ADAPTER_CONFLICT", Assert.Throws<ClientFault>(() =>
            OutboundInterface.Select(identity, [new(identity, "Renamed adapter", true, NetworkInterfaceType.Ethernet)])).Code);
    }

    [Theory]
    [InlineData("1.1.1.1", 2)]
    [InlineData("2606:4700:4700::1111", 23)]
    public void RouteLookupUsesDestinationFamilyAndNetworkOrderAddressBytes(string host, int family)
    {
        var address = IPAddress.Parse(host);
        var value = OutboundInterface.CreateAddress(address);
        var bytes = new byte[28];
        MemoryMarshal.Write(bytes, in value);
        Assert.Equal(family, BinaryPrimitives.ReadUInt16LittleEndian(bytes));
        Assert.Equal(address.GetAddressBytes(), bytes.AsSpan(family == 2 ? 4 : 8, family == 2 ? 4 : 16).ToArray());
        Assert.Equal(28, Marshal.SizeOf<OutboundInterface.SocketAddress>());
        Assert.Equal(104, Marshal.SizeOf<OutboundInterface.RouteRow>());
        Assert.Equal(8, Marshal.OffsetOf<OutboundInterface.RouteRow>(nameof(OutboundInterface.RouteRow.Index)).ToInt32());
    }

    [Fact]
    public void ExplicitInterfaceIsSerializedAsDataWithoutChangingRoutingOrDnsProtection()
    {
        var plan = CoreFixtures.Plan();
        using var baseline = JsonDocument.Parse(XrayConfigBuilder.Build(plan, 18899, "probeUser00000000", "probePassword0000"));
        using var bound = JsonDocument.Parse(XrayConfigBuilder.Build(plan, 18899, "probeUser00000000", "probePassword0000", "Ethernet 2"));
        Assert.Equal("Ethernet 2", bound.RootElement.GetProperty("inbounds")[0].GetProperty("settings")
            .GetProperty("autoOutboundsInterface").GetString());
        Assert.Equal(baseline.RootElement.GetProperty("routing").GetRawText(), bound.RootElement.GetProperty("routing").GetRawText());
        Assert.Equal(baseline.RootElement.GetProperty("dns").GetRawText(), bound.RootElement.GetProperty("dns").GetRawText());
        Assert.Equal("INVALID_PLAN", Assert.Throws<ClientFault>(() =>
            XrayConfigBuilder.Build(plan, 18899, "probeUser00000000", "probePassword0000", "Ethernet\0")).Code);
    }

    private static void AssertUnavailable(Action action)
        => Assert.Equal("OUTBOUND_INTERFACE_UNAVAILABLE", Assert.Throws<ClientFault>(action).Code);
}
