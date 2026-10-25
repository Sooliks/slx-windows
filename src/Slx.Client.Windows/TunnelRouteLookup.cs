using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Slx.Client.Core;

namespace Slx.Client.Windows;

/// <summary>Read the unrestricted Windows route; never bind sockets or mutate another route.</summary>
internal static class TunnelRouteLookup
{
    [DllImport("iphlpapi.dll", ExactSpelling = true)]
    private static extern uint GetBestRoute2(nint interfaceLuid, uint interfaceIndex, nint sourceAddress,
        in OutboundInterface.SocketAddress destination, uint options, out OutboundInterface.RouteRow route,
        out OutboundInterface.SocketAddress source);

    [DllImport("iphlpapi.dll", ExactSpelling = true)]
    private static extern uint ConvertInterfaceLuidToGuid(in ulong luid, out Guid guid);

    internal static DnsRouteObservation Observe(string ownedAdapterId, IPAddress destination)
        => Observe(ownedAdapterId, destination, ReadRoute, ReadIdentity);

    internal static DnsRouteObservation Observe(string ownedAdapterId, IPAddress destination,
        Func<IPAddress, (uint Error, ulong Luid, byte PrefixLength, uint Metric)> readRoute,
        Func<ulong, (uint Error, Guid Identity)> readIdentity)
    {
        if (!Guid.TryParse(ownedAdapterId, out var expected) || expected != OutboundInterface.TunnelIdentity
            || destination is null || destination.IsIPv4MappedToIPv6 || !EndpointPolicy.IsPublicAddress(destination))
            return new(DnsRouteStatus.Unavailable);
        int maxPrefix = destination.AddressFamily == AddressFamily.InterNetwork ? 32 : 128;
        var route = readRoute(destination);
        if (route.Error != 0) return new(DnsRouteStatus.Unavailable, route.Error);
        if (route.Luid == 0 || route.PrefixLength > maxPrefix) return new(DnsRouteStatus.Unavailable);
        var adapter = readIdentity(route.Luid);
        if (adapter.Error != 0) return new(DnsRouteStatus.Unavailable, adapter.Error);
        if (adapter.Identity == Guid.Empty) return new(DnsRouteStatus.Unavailable);
        return new(adapter.Identity == expected ? DnsRouteStatus.OwnedTunnel : DnsRouteStatus.OtherInterface,
            PrefixLength: route.PrefixLength, RouteMetric: route.Metric);
    }

    private static (uint Error, ulong Luid, byte PrefixLength, uint Metric) ReadRoute(IPAddress destination)
    {
        var address = OutboundInterface.CreateAddress(destination);
        uint error = GetBestRoute2(0, 0, 0, in address, 0, out var route, out _);
        return error == 0 ? (0, route.Luid, route.PrefixLength, route.Metric) : (error, 0, 0, 0);
    }

    private static (uint Error, Guid Identity) ReadIdentity(ulong luid)
    {
        uint error = ConvertInterfaceLuidToGuid(in luid, out var identity);
        return (error, identity);
    }
}
