using System.Net;
using Slx.Client.Core;

namespace Slx.Client.Windows;

/// <summary>Read ordinary public IPv4/IPv6 routes independently of the protected DNS host routes.</summary>
public static class TunnelDataRoute
{
    public static DnsRouteObservation Observe(string ownedAdapterId, IPAddress destination)
        => TunnelRouteLookup.Observe(ownedAdapterId, destination);
}
