using System.Buffers.Binary;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using Slx.Client.Core;

namespace Slx.Client.Windows;

/// <summary>Read the Windows route before creating our TUN; never change another adapter or route.</summary>
public static class OutboundInterface
{
    // Pinned Xray v26.9.9 derives the Windows adapter GUID from the UTF-8 name "SLX VPN".
    // A user-renamed stale adapter must not become our outbound route back into the TUN.
    internal static readonly Guid TunnelIdentity = new("c5656347-93ca-8b73-7336-a92e8199b47b");
    [StructLayout(LayoutKind.Explicit, Size = 28)]
    internal struct SocketAddress
    {
        [FieldOffset(0)] public ushort Family;
        [FieldOffset(4)] public uint Ipv4;
        [FieldOffset(8)] public ulong First;
        [FieldOffset(16)] public ulong Last;
    }

    // MIB_IPFORWARD_ROW2 is 104 bytes on supported Windows x64; DestinationPrefix starts at 12.
    // IP_ADDRESS_PREFIX.PrefixLength is at +28; route Metric is at 84 (winipcfg v1.0.1 types_test.go).
    [StructLayout(LayoutKind.Explicit, Size = 104)]
    internal struct RouteRow
    {
        [FieldOffset(0)] public ulong Luid;
        [FieldOffset(8)] public uint Index;
        [FieldOffset(40)] public byte PrefixLength;
        [FieldOffset(84)] public uint Metric;
    }

    [DllImport("iphlpapi.dll", ExactSpelling = true)]
    private static extern uint GetBestRoute2(nint interfaceLuid, uint interfaceIndex, nint sourceAddress,
        in SocketAddress destinationAddress, uint addressSortOptions, out RouteRow route, out SocketAddress source);
    [DllImport("iphlpapi.dll", ExactSpelling = true)]
    private static extern uint ConvertInterfaceLuidToGuid(in ulong luid, out Guid guid);

    public static string ForEndpoint(string pinnedAddress)
    {
        if (!IPAddress.TryParse(pinnedAddress, out var address) || !EndpointPolicy.IsPublicAddress(address))
            throw new ClientFault("ENDPOINT_NOT_PUBLIC");
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        var destination = CreateAddress(address);
        if (GetBestRoute2(0, 0, 0, in destination, 0, out var route, out _) != 0 || route.Luid == 0
            || ConvertInterfaceLuidToGuid(in route.Luid, out var identity) != 0)
            throw new ClientFault("OUTBOUND_INTERFACE_UNAVAILABLE");
        try
        {
            return Select(identity, NetworkInterface.GetAllNetworkInterfaces().Select(adapter =>
                new Candidate(Guid.TryParse(adapter.Id, out var id) ? id : Guid.Empty, adapter.Name,
                    adapter.OperationalStatus == OperationalStatus.Up, adapter.NetworkInterfaceType)));
        }
        catch (NetworkInformationException) { throw new ClientFault("OUTBOUND_INTERFACE_UNAVAILABLE"); }
    }

    internal static SocketAddress CreateAddress(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return bytes.Length == 4
            ? new() { Family = 2, Ipv4 = BinaryPrimitives.ReadUInt32LittleEndian(bytes) }
            : new() { Family = 23, First = BinaryPrimitives.ReadUInt64LittleEndian(bytes),
                Last = BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(8)) };
    }

    internal static string Select(Guid routeIdentity, IEnumerable<Candidate> candidates)
    {
        if (routeIdentity == TunnelIdentity) throw new ClientFault("TUN_ADAPTER_CONFLICT");
        var matches = candidates.Where(adapter => adapter.Id == routeIdentity).Take(2).ToArray();
        if (routeIdentity == Guid.Empty || matches.Length != 1 || !matches[0].Up
            || matches[0].Type == NetworkInterfaceType.Loopback)
            throw new ClientFault("OUTBOUND_INTERFACE_UNAVAILABLE");
        string name = matches[0].Name;
        if (string.Equals(name, "SLX VPN", StringComparison.OrdinalIgnoreCase))
            throw new ClientFault("TUN_ADAPTER_CONFLICT");
        // "auto" has special meaning in the pinned core and is never an explicit interface choice.
        if (string.IsNullOrWhiteSpace(name) || name.Length > 256 || name.Any(char.IsControl) || name == "auto")
            throw new ClientFault("OUTBOUND_INTERFACE_UNAVAILABLE");
        return name;
    }

    internal sealed record Candidate(Guid Id, string Name, bool Up, NetworkInterfaceType Type);
}
