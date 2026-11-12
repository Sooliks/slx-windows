using System.Text.Json.Serialization;
namespace Slx.Client.Core;
public enum ProbeTarget { Gstatic, Slx }
public enum ProbeOutcome { Success, HttpRejected, Timeout, NameResolution, Connection, Tls, Proxy, Protocol, Other }
public sealed record ProbeObservation(ProbeTarget Target, ProbeOutcome Outcome, int ElapsedMs, int? HttpStatus = null)
{
    public bool Passed => Outcome == ProbeOutcome.Success;
}
/// <param name="Established">The same request on a connection that was already open through the VPN core. Present
/// only when that connection was genuinely reused; a freshly opened one is not evidence about existing streams.</param>
/// <param name="UpstreamHolds">Checks that kept the core because only the established probe stream answered.</param>
public sealed record HealthCheckObservation(int Incidents, int RetainedConnections, DateTimeOffset At,
    ProbeObservation Primary, ProbeObservation? Repeat, ProbeObservation? Alternate,
    // The service omits these observations while off for the strict decoder in previously staged updaters.
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ProbeObservation? Established = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? UpstreamHolds = null);
