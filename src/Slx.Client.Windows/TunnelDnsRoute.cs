using System.Net;
using System.Net.Sockets;
using Slx.Client.Core;

namespace Slx.Client.Windows;

/// <summary>Observe ordinary IPv4 DNS routing without binding a socket or changing a route.</summary>
public static class TunnelDnsRoute
{
    public static DnsRouteObservation Observe(string ownedAdapterId, IPAddress destination)
        => destination?.AddressFamily == AddressFamily.InterNetwork
            ? TunnelRouteLookup.Observe(ownedAdapterId, destination) : new(DnsRouteStatus.Unavailable);

    internal static DnsRouteObservation Observe(string ownedAdapterId, IPAddress destination,
        Func<IPAddress, (uint Error, ulong Luid, byte PrefixLength, uint Metric)> readRoute,
        Func<ulong, (uint Error, Guid Identity)> readIdentity)
        => destination?.AddressFamily == AddressFamily.InterNetwork
            ? TunnelRouteLookup.Observe(ownedAdapterId, destination, readRoute, readIdentity)
            : new(DnsRouteStatus.Unavailable);
}
