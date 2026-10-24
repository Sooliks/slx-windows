using System.Text.Json;
using Slx.Client.Core;
using Slx.Client.Service;
using Slx.Client.Windows;
using Xunit;
namespace Slx.Client.Tests;
public sealed class RecoveryObservationTests
{
    [Fact]
    public async Task OldStatusWithoutDiagnosticsRemainsReadable()
    {
        using var stream = new MemoryStream();
        await PipeFraming.WriteAsync(stream, new { Phase = 2, ObservedAt = "2026-10-19T13:00:00Z", ProbePassed = true });
        stream.Position = 0;
        var status = await PipeFraming.ReadAsync<TunnelStatus>(stream);
        Assert.Equal(TunnelPhase.Connected,status.Phase);Assert.True(status.ProbePassed);Assert.Null(status.Recovery);Assert.Null(status.HealthCheck);Assert.Null(status.DnsCheck);
    }
    [Fact]
    public void RecoveryReasonSurvivesStatusReadsWithoutClaimingConnectionSuccess()
    {
        var engine=new TunnelEngine();var before=DateTimeOffset.UtcNow;
        engine.RecordRecovery(RecoveryCause.ProbeFailed);
        engine.RecordRecovery(RecoveryCause.CoreExited);
        var status=engine.Status;
        Assert.Equal(TunnelPhase.Disconnected,status.Phase);Assert.False(status.ProbePassed);
        Assert.Null(status.ConnectedAt);Assert.Equal(2,status.Recovery!.Count);
        Assert.Equal(RecoveryCause.CoreExited,status.Recovery.Cause);
        Assert.InRange(status.Recovery.At,before,DateTimeOffset.UtcNow);
        Assert.Equal(status.Recovery,engine.Status.Recovery);
        Assert.Equal(status,JsonSerializer.Deserialize<TunnelStatus>(JsonSerializer.Serialize(status)));
    }
    [Fact]
    public async Task DnsFailureSurvivesStatusReadsWithoutClaimingConnectionSuccess()
    {
        var engine = new TunnelEngine();
        var observation = new DnsCheckObservation(DateTimeOffset.UtcNow,
            new(ProbeTarget.Gstatic, DnsProbeOutcome.Timeout, 3000, Route: new(DnsRouteStatus.OwnedTunnel)),
            new(ProbeTarget.Slx, DnsProbeOutcome.SocketError, 2, SocketError: 10013,
                Route: new(DnsRouteStatus.OwnedTunnel)));
        engine.RecordDnsCheck(observation);
        var status = engine.Status;
        Assert.Equal(TunnelPhase.Disconnected, status.Phase);
        Assert.False(status.ProbePassed); Assert.False(status.ProtectionActive);
        Assert.Null(status.ConnectedAt); Assert.Equal(observation, status.DnsCheck);
        Assert.Same(observation, engine.Status.DnsCheck);
        var reply = new PipeReply(false, "VPN_DNS_FAILED", status);
        using var stream = new MemoryStream();
        await PipeFraming.WriteAsync(stream, reply);
        Assert.InRange(stream.Length, 1, PipeContract.MaxBytes);
        stream.Position = 0;
        Assert.Equal(reply, await PipeFraming.ReadAsync<PipeReply>(stream));
    }
}
