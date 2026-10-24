using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Slx.Client.Core;

namespace Slx.Client.Windows;

/// <summary>Observe ordinary Windows DNS routing without binding a socket or changing a route.</summary>
public static class TunnelDnsRoute
{
    [DllImport("iphlpapi.dll", ExactSpelling = true)]
    private static extern uint GetBestRoute2(nint interfaceLuid, uint interfaceIndex, nint sourceAddress,
        in OutboundInterface.SocketAddress destination, uint options, out OutboundInterface.RouteRow route,
        out OutboundInterface.SocketAddress source);

    [DllImport("iphlpapi.dll", ExactSpelling = true)]
    private static extern uint ConvertInterfaceLuidToGuid(in ulong luid, out Guid guid);

    public static DnsRouteObservation Observe(string ownedAdapterId, IPAddress destination)
        => Observe(ownedAdapterId, destination, ReadRoute, ReadIdentity);

    internal static DnsRouteObservation Observe(string ownedAdapterId, IPAddress destination,
        Func<IPAddress, (uint Error, ulong Luid)> readRoute,
        Func<ulong, (uint Error, Guid Identity)> readIdentity)
    {
        if (!Guid.TryParse(ownedAdapterId, out var expected) || expected != OutboundInterface.TunnelIdentity
            || destination is null || destination.AddressFamily != AddressFamily.InterNetwork
            || !EndpointPolicy.IsPublicAddress(destination))
            return new(DnsRouteStatus.Unavailable);

        var route = readRoute(destination);
        if (route.Error != 0) return new(DnsRouteStatus.Unavailable, route.Error);
        if (route.Luid == 0) return new(DnsRouteStatus.Unavailable);

        var adapter = readIdentity(route.Luid);
        if (adapter.Error != 0) return new(DnsRouteStatus.Unavailable, adapter.Error);
        if (adapter.Identity == Guid.Empty) return new(DnsRouteStatus.Unavailable);

        return new(adapter.Identity == expected ? DnsRouteStatus.OwnedTunnel : DnsRouteStatus.OtherInterface);
    }

    private static (uint Error, ulong Luid) ReadRoute(IPAddress destination)
    {
        var address = OutboundInterface.CreateAddress(destination);
        // An interface/source restriction would hide a competing ordinary Windows route.
        uint error = GetBestRoute2(0, 0, 0, in address, 0, out var route, out _);
        return (error, route.Luid);
    }

    private static (uint Error, Guid Identity) ReadIdentity(ulong luid)
    {
        uint error = ConvertInterfaceLuidToGuid(in luid, out var identity);
        return (error, identity);
    }
}
