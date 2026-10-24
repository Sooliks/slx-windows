using System.Net.Sockets;
using System.Text.Json;
using Slx.Client.Core;
using Slx.Client.Service;
using Xunit;

namespace Slx.Client.Tests;

public sealed class TunnelDnsProbeTests
{
    private static DnsRouteObservation Owned() => new(DnsRouteStatus.OwnedTunnel);

    [Fact]
    public async Task VerifiedFirstResponseRechecksRouteAndStopsFurtherAttempts()
    {
        int sends = 0, routes = 0;
        var observations = new List<DnsCheckObservation>();
        await TunnelDnsProbe.CheckAsync((query, _) =>
        { sends++; return Task.FromResult(DnsProbeFixtures.Reply(query)); },
            () => { routes++; return Owned(); }, observations.Add, default);
        Assert.Equal(1, sends); Assert.Equal(2, routes);
        var result = Assert.Single(observations);
        Assert.True(result.First.Passed); Assert.Equal(0, result.First.ResponseCode);
        Assert.Equal(DnsRouteStatus.OwnedTunnel, result.First.Route!.Status);
        Assert.Null(result.Second); Assert.Null(result.Third);
    }

    [Fact]
    public async Task OneTimeoutDoesNotDiscardASecondVerifiedHostResponse()
    {
        int sends = 0;
        var observations = new List<DnsCheckObservation>();
        await TunnelDnsProbe.CheckAsync((query, _) => ++sends == 1
            ? Task.FromException<byte[]>(new OperationCanceledException("upstream detail"))
            : Task.FromResult(DnsProbeFixtures.Reply(query)), Owned, observations.Add, default);
        Assert.Equal(2, sends); Assert.Equal(2, observations.Count);
        Assert.Equal(DnsProbeOutcome.Timeout, observations[1].First.Outcome);
        Assert.Equal(ProbeTarget.Gstatic, observations[1].First.Target);
        Assert.True(observations[1].Second!.Passed);
        Assert.Equal(ProbeTarget.Slx, observations[1].Second!.Target);
        Assert.Null(observations[0].Second); Assert.Null(observations[1].Third);
        Assert.DoesNotContain("upstream detail", JsonSerializer.Serialize(observations));
    }

    [Fact]
    public async Task AResponseReturnedAfterItsOwnDeadlineCannotPass()
    {
        int sends = 0;
        var observations = new List<DnsCheckObservation>();
        async Task<byte[]> Exchange(byte[] query, CancellationToken token)
        {
            if (++sends == 1)
            {
                try { await Task.Delay(Timeout.InfiniteTimeSpan, token).WaitAsync(TimeSpan.FromSeconds(10)); }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            }
            return DnsProbeFixtures.Reply(query); // Simulate completion racing the cancelled socket operation.
        }
        await TunnelDnsProbe.CheckAsync(Exchange, Owned, observations.Add, default);
        Assert.Equal(2, sends);
        Assert.Equal(DnsProbeOutcome.Timeout, observations[^1].First.Outcome);
        Assert.True(observations[^1].Second!.Passed);
    }

    [Theory]
    [InlineData(DnsProbeOutcome.Timeout)]
    [InlineData(DnsProbeOutcome.SocketError)]
    [InlineData(DnsProbeOutcome.Rejected)]
    [InlineData(DnsProbeOutcome.InvalidResponse)]
    public async Task FailureKeepsThreeBoundedTypedObservations(DnsProbeOutcome outcome)
    {
        var observations = new List<DnsCheckObservation>();
        Task<byte[]> Exchange(byte[] query, CancellationToken _) => outcome switch
        {
            DnsProbeOutcome.Timeout => Task.FromException<byte[]>(new OperationCanceledException("private detail")),
            DnsProbeOutcome.SocketError => Task.FromException<byte[]>(new SocketException((int)SocketError.AccessDenied)),
            DnsProbeOutcome.Rejected => Task.FromResult(DnsProbeFixtures.Reply(query, 2, false)),
            _ => Task.FromResult(DnsProbeFixtures.Reply(query)[..12])
        };
        var fault = await Assert.ThrowsAsync<ClientFault>(() =>
            TunnelDnsProbe.CheckAsync(Exchange, Owned, observations.Add, default));
        Assert.Equal("VPN_DNS_FAILED", fault.Code); Assert.Equal(3, observations.Count);
        var last = observations[^1];
        Assert.All(new[] { last.First, last.Second!, last.Third! }, item =>
        {
            Assert.Equal(outcome, item.Outcome); Assert.False(item.Passed);
            Assert.Equal(outcome == DnsProbeOutcome.SocketError ? (int?)SocketError.AccessDenied : null, item.SocketError);
            Assert.Equal(outcome == DnsProbeOutcome.Rejected ? 2 : (int?)null, item.ResponseCode);
        });
        Assert.DoesNotContain("private detail", JsonSerializer.Serialize(last));
    }

    [Theory]
    [InlineData(DnsRouteStatus.OtherInterface, DnsProbeOutcome.RouteMismatch)]
    [InlineData(DnsRouteStatus.Unavailable, DnsProbeOutcome.RouteUnavailable)]
    public async Task UnconfirmedRouteCannotSendDnsOrConfirmReadiness(DnsRouteStatus route, DnsProbeOutcome expected)
    {
        int sends = 0;
        var observations = new List<DnsCheckObservation>();
        await Assert.ThrowsAsync<ClientFault>(() => TunnelDnsProbe.CheckAsync((query, _) =>
        { sends++; return Task.FromResult(DnsProbeFixtures.Reply(query)); },
            () => new(route, route == DnsRouteStatus.Unavailable ? 1168u : null), observations.Add, default));
        Assert.Equal(0, sends); Assert.Equal(3, observations.Count);
        Assert.Equal(expected, observations[^1].Third!.Outcome);
        Assert.Equal(route, observations[^1].Third!.Route!.Status);
        if (route == DnsRouteStatus.Unavailable) Assert.Equal(1168u, observations[^1].Third!.Route!.NativeError);
    }

    [Fact]
    public async Task RouteChangingDuringExchangeRejectsEvenAValidAnswer()
    {
        int routes = 0, sends = 0;
        var observations = new List<DnsCheckObservation>();
        await Assert.ThrowsAsync<ClientFault>(() => TunnelDnsProbe.CheckAsync((query, _) =>
        { sends++; return Task.FromResult(DnsProbeFixtures.Reply(query)); },
            () => ++routes == 1 ? Owned() : new(DnsRouteStatus.OtherInterface), observations.Add, default));
        Assert.Equal(1, sends);
        Assert.Equal(DnsProbeOutcome.RouteMismatch, observations[0].First.Outcome);
        Assert.Equal(0, observations[0].First.ResponseCode);
        Assert.All(observations, item => Assert.False(item.First.Passed));
    }

    [Theory]
    [InlineData("before")]
    [InlineData("route")]
    [InlineData("exchange-result")]
    [InlineData("exchange-throws")]
    [InlineData("response-route")]
    public async Task CallerCancellationCannotBecomeSuccessOrFailureObservation(string when)
    {
        using var cancellation = new CancellationTokenSource();
        int sends = 0, routes = 0;
        var observations = new List<DnsCheckObservation>();
        if (when == "before") cancellation.Cancel();
        Task<byte[]> Exchange(byte[] query, CancellationToken token)
        {
            sends++;
            if (when is "exchange-result" or "exchange-throws") cancellation.Cancel();
            if (when == "exchange-throws") return Task.FromCanceled<byte[]>(token);
            return Task.FromResult(DnsProbeFixtures.Reply(query));
        }
        DnsRouteObservation Route()
        {
            routes++;
            if (when == "route" || (when == "response-route" && routes == 2)) cancellation.Cancel();
            return Owned();
        }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            TunnelDnsProbe.CheckAsync(Exchange, Route, observations.Add, cancellation.Token));
        Assert.Empty(observations);
        Assert.Equal(when is "before" or "route" ? 0 : 1, sends);
    }
}
