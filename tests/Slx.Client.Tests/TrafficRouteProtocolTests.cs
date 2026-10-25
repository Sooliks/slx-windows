using System.Text;
using Slx.Client.Core;
using Slx.Client.Windows;
using Xunit;
namespace Slx.Client.Tests;
public sealed class TrafficRouteProtocolTests
{
    [Fact]
    public async Task FixedRouteEvidenceRoundTripsWithoutClaimingProbeSuccess()
    {
        var routes = new TrafficRouteCheckObservation(DateTimeOffset.UtcNow,
            new(DnsRouteStatus.OtherInterface, PrefixLength: 1, RouteMetric: 5),
            new(DnsRouteStatus.OwnedTunnel, PrefixLength: 1, RouteMetric: 0),
            new(DnsRouteStatus.Unavailable, NativeError: 1168));
        var reply = new PipeReply(true, "OK", new(TunnelPhase.Reconnecting, DateTimeOffset.UtcNow,
            Code: "VPN_TRAFFIC_ROUTE_FAILED", ProtectionActive: true, TrafficRoutes: routes));
        using var stream = new MemoryStream();
        await PipeFraming.WriteAsync(stream, reply);
        var wire = Encoding.UTF8.GetString(stream.ToArray()[4..]);
        Assert.DoesNotContain("8.8.8.8", wire);
        Assert.DoesNotContain("142.250.0.1", wire);
        Assert.DoesNotContain("2001:4860", wire);
        stream.Position = 0;
        var read = await PipeFraming.ReadAsync<PipeReply>(stream);
        Assert.Equal(reply, read);
        Assert.False(read.Status.ProbePassed);
        Assert.True(read.Status.ProtectionActive);
    }
    [Fact]
    public async Task OldServiceObservationWithoutRouteFieldRemainsReadable()
    {
        var payload = new { Success = true, Code = "OK", Status = new
        { Phase = TunnelPhase.Disconnected, ObservedAt = DateTimeOffset.UtcNow } };
        using var stream = new MemoryStream();
        await PipeFraming.WriteAsync(stream, payload);
        stream.Position = 0;
        Assert.Null((await PipeFraming.ReadAsync<PipeReply>(stream)).Status.TrafficRoutes);
    }
}
