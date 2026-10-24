using Slx.Client.Core;
namespace Slx.Client.Service;
internal sealed partial class TunnelEngine
{
    private volatile RecoveryObservation? recovery;
    private volatile HealthCheckObservation? healthCheck;
    private volatile DnsCheckObservation? dnsCheck;
    internal void RecordDnsCheck(DnsCheckObservation observation) => dnsCheck = observation;
    internal void RecordRecovery(RecoveryCause cause)
    {
        var count=Math.Min(recovery?.Count??0,int.MaxValue-1)+1;
        recovery=new(count,cause,DateTimeOffset.UtcNow);
    }
    internal void RecordHealthIncident(HealthConfirmationResult result, bool retained)
    {
        if (result.Primary.Passed) return;
        healthCheck = new(Math.Min(healthCheck?.Incidents ?? 0, int.MaxValue-1)+1,
            (int)Math.Min((long)(healthCheck?.RetainedConnections ?? 0)+(retained ? 1 : 0), int.MaxValue),
            DateTimeOffset.UtcNow, result.Primary, result.Repeat, result.Alternate);
    }
}
