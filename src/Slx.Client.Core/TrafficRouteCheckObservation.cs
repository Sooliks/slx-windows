namespace Slx.Client.Core;

/// <summary>Fixed public data-route samples; null IPv6 means no usable public IPv6 source was present.</summary>
public sealed record TrafficRouteCheckObservation(DateTimeOffset At, DnsRouteObservation Ipv4Low,
    DnsRouteObservation Ipv4High, DnsRouteObservation? Ipv6 = null);
