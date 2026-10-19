namespace Slx.Client.Core;
public enum ProbeTarget { Gstatic, Slx }
public enum ProbeOutcome { Success, HttpRejected, Timeout, NameResolution, Connection, Tls, Proxy, Protocol, Other }
public sealed record ProbeObservation(ProbeTarget Target, ProbeOutcome Outcome, int ElapsedMs, int? HttpStatus = null)
{
    public bool Passed => Outcome == ProbeOutcome.Success;
}
public sealed record HealthCheckObservation(int Incidents, int RetainedConnections, DateTimeOffset At,
    ProbeObservation Primary, ProbeObservation? Repeat, ProbeObservation? Alternate);
