namespace Slx.Client.Core;

public enum DnsRouteStatus { OwnedTunnel, OtherInterface, Unavailable }
public sealed record DnsRouteObservation(DnsRouteStatus Status, uint? NativeError = null);
public enum DnsProbeOutcome
{
    Success, Timeout, SocketError, Rejected, InvalidResponse, RouteMismatch, RouteUnavailable
}
public sealed record DnsProbeObservation(ProbeTarget Target, DnsProbeOutcome Outcome, int ElapsedMs,
    int? ResponseCode = null, int? SocketError = null, DnsRouteObservation? Route = null)
{
    public bool Passed => Outcome == DnsProbeOutcome.Success;
}
public sealed record DnsCheckObservation(DateTimeOffset At, DnsProbeObservation First,
    DnsProbeObservation? Second = null, DnsProbeObservation? Third = null);
