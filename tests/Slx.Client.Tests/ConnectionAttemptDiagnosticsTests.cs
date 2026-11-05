using System.Text.Json;
using Slx.Client.Core;
using Slx.Client.Service;
using Slx.Client.Windows;
using Xunit;

namespace Slx.Client.Tests;

public sealed class ConnectionAttemptDiagnosticsTests
{
    [Fact]
    public async Task IdleStatusKeepsTheWireShapeOlderStrictUpdatersReadAndActiveStatusRoundTrips()
    {
        var idle = new TunnelStatus(TunnelPhase.Disconnected, DateTimeOffset.UtcNow);
        Assert.DoesNotContain("Attempt", JsonSerializer.Serialize(idle), StringComparison.Ordinal);
        var active = idle with { Phase = TunnelPhase.Reconnecting,
            Attempt = new("SLX · Test", "vless-reality", 2, 2, 3, "VPN_PROBE_FAILED", DateTimeOffset.UtcNow) };
        var reply = new PipeReply(true, "OK", active);
        using var stream = new MemoryStream();
        await PipeFraming.WriteAsync(stream, reply);
        stream.Position = 0;
        Assert.Equal(reply, await PipeFraming.ReadAsync<PipeReply>(stream));
    }

    [Fact]
    public void SingleFailedAttemptThatTheRepeatPassesIsNotAnIncident()
    {
        var engine = new TunnelEngine();
        var tls = new ProbeObservation(ProbeTarget.Gstatic, ProbeOutcome.Tls, 2420);
        var success = new ProbeObservation(ProbeTarget.Gstatic, ProbeOutcome.Success, 709, 204);
        var timeout = new ProbeObservation(ProbeTarget.Gstatic, ProbeOutcome.Timeout, 3000);
        engine.RecordHealthIncident(new(tls, success, success), true);
        Assert.Equal((0, 0), (engine.Status.HealthCheck!.Incidents, engine.Status.HealthCheck.RetainedConnections));
        Assert.Equal(ProbeOutcome.Tls, engine.Status.HealthCheck.Primary.Outcome);
        engine.RecordHealthIncident(new(timeout, timeout, success), true);
        engine.RecordHealthIncident(new(timeout, timeout, timeout), false);
        Assert.Equal((2, 1), (engine.Status.HealthCheck!.Incidents, engine.Status.HealthCheck.RetainedConnections));
        Assert.Null(engine.Status.Attempt);
    }
}
