using System.Buffers.Binary;
using System.Runtime.InteropServices;
using Slx.Client.Windows;
using Xunit;

namespace Slx.Client.Tests;

public sealed class TunnelDnsRouteLayoutTests
{
    // The pinned Xray dependency uses these native offsets and verifies them in its own tests:
    // github.com/WireGuard/wireguard-windows/blob/v1.0.1/tunnel/winipcfg/types_test.go
    [Fact]
    public void NativeRouteFieldsMatchWindowsX64RowAndNestedPrefixLayout()
    {
        Assert.Equal(104, Marshal.SizeOf<OutboundInterface.RouteRow>());
        Assert.Equal(0, Offset(nameof(OutboundInterface.RouteRow.Luid)));
        Assert.Equal(8, Offset(nameof(OutboundInterface.RouteRow.Index)));
        Assert.Equal(12 + 28, Offset(nameof(OutboundInterface.RouteRow.PrefixLength)));
        Assert.Equal(84, Offset(nameof(OutboundInterface.RouteRow.Metric)));
    }

    [Fact]
    public void NativeByteFixtureReadsPrefixAndMetricWithoutConfusingNeighbouringFields()
    {
        var bytes = Enumerable.Repeat((byte)0xcc, 104).ToArray();
        BinaryPrimitives.WriteUInt64LittleEndian(bytes, 0x0011223344556677UL);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), 73);
        bytes[40] = 32;
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(84), 420);
        var row = MemoryMarshal.Read<OutboundInterface.RouteRow>(bytes);
        Assert.Equal(0x0011223344556677UL, row.Luid);
        Assert.Equal(73U, row.Index);
        Assert.Equal((byte)32, row.PrefixLength);
        Assert.Equal(420U, row.Metric);
    }

    private static int Offset(string name) => Marshal.OffsetOf<OutboundInterface.RouteRow>(name).ToInt32();
}
