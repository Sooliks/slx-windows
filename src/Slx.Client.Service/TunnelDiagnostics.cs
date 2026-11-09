using Slx.Client.Core;
namespace Slx.Client.Service;
internal sealed partial class TunnelEngine
{
    private volatile RecoveryObservation? recovery;
    private volatile HealthCheckObservation? healthCheck;
    private volatile DnsCheckObservation? dnsCheck;
    private volatile TrafficRouteCheckObservation? trafficRoutes;
    private sealed record HealthSuccess(DateTimeOffset At);
    private volatile HealthSuccess? lastHealthy;
    private DateTimeOffset? lastHealthyAt { get => lastHealthy?.At; set => lastHealthy = value is { } at ? new(at) : null; }
    internal void RecordDnsCheck(DnsCheckObservation observation) => dnsCheck = observation;
    internal void RecordTrafficRoutes(TrafficRouteCheckObservation observation) => trafficRoutes = observation;
    internal void RecordRecovery(RecoveryCause cause)
    {
        var count=Math.Min(recovery?.Count??0,int.MaxValue-1)+1;
        recovery=new(count,cause,DateTimeOffset.UtcNow);
    }
    internal void RecordHealthIncident(HealthConfirmationResult result, bool retained)
    {
        if (retained) lastHealthyAt = DateTimeOffset.UtcNow;
        if (result.Primary.Passed) return;
        // One failed TLS/connection attempt that the repeat check immediately passes is a blip: its details stay
        // visible, but it is not an incident. Only a failed repeat makes the connection genuinely doubtful.
        bool blip = result.Repeat?.Passed == true;
        int incidents = healthCheck?.Incidents ?? 0, retainedCount = healthCheck?.RetainedConnections ?? 0;
        healthCheck = new(blip ? incidents : Math.Min(incidents, int.MaxValue-1)+1,
            blip ? retainedCount : (int)Math.Min((long)retainedCount+(retained ? 1 : 0), int.MaxValue),
            DateTimeOffset.UtcNow, result.Primary, result.Repeat, result.Alternate);
    }
}
