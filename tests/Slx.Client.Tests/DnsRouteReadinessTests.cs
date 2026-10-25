using Slx.Client.Core;
using Slx.Client.Service;
using Xunit;

namespace Slx.Client.Tests;

public sealed class DnsRouteReadinessTests
{
    [Theory]
    [InlineData(DnsRouteStatus.OtherInterface)]
    [InlineData(DnsRouteStatus.Unavailable)]
    public async Task ASettlingRouteWaitsBeforeSendingAndKeepsTheFirstDnsAttempt(DnsRouteStatus initial)
    {
        var paused = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var released = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var observations = new List<DnsCheckObservation>();
        bool available = false;
        int sends = 0;
        Task<byte[]> Exchange(byte[] query, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            Assert.True(available);
            sends++;
            return Task.FromResult(DnsProbeFixtures.Reply(query));
        }
        async Task Pause(TimeSpan delay, CancellationToken token)
        {
            Assert.InRange(delay.TotalMilliseconds, 1, 3000);
            paused.TrySetResult();
            await released.Task.WaitAsync(token);
        }
        var check = TunnelDnsProbe.CheckAsync(Exchange,
            () => new(available ? DnsRouteStatus.OwnedTunnel : initial), observations.Add, default, Pause);
        await paused.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.False(check.IsCompleted);
        Assert.Equal(0, sends);
        Assert.Empty(observations);
        available = true;
        released.SetResult();
        await check.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(1, sends);
        var result = Assert.Single(observations);
        Assert.True(result.First.Passed);
        Assert.Equal(ProbeTarget.Gstatic, result.First.Target);
        Assert.Equal(DnsRouteStatus.OwnedTunnel, result.First.Route!.Status);
        Assert.Null(result.Second);
    }

    [Fact]
    public async Task UnavailableThenForeignRouteCannotSkipWaitingForTheOwnedTunnel()
    {
        var routes = new Queue<DnsRouteObservation>([
            new(DnsRouteStatus.Unavailable, 1168), new(DnsRouteStatus.OtherInterface),
            new(DnsRouteStatus.OwnedTunnel), new(DnsRouteStatus.OwnedTunnel)]);
        var observations = new List<DnsCheckObservation>();
        int sends = 0, pauses = 0;
        await TunnelDnsProbe.CheckAsync((query, _) =>
        { sends++; Assert.Equal(2, pauses); return Task.FromResult(DnsProbeFixtures.Reply(query)); },
            () => routes.Dequeue(), observations.Add, default, (_, token) =>
            {
                token.ThrowIfCancellationRequested();
                Assert.Equal(0, sends);
                pauses++;
                return Task.CompletedTask;
            });
        Assert.Empty(routes);
        Assert.Equal(1, sends);
        Assert.True(Assert.Single(observations).First.Passed);
    }

    [Theory]
    [InlineData(DnsRouteStatus.OtherInterface, DnsProbeOutcome.RouteMismatch)]
    [InlineData(DnsRouteStatus.Unavailable, DnsProbeOutcome.RouteUnavailable)]
    public async Task PersistentRouteFailureHasBoundedWaitsAndNeverSendsDns(
        DnsRouteStatus status, DnsProbeOutcome outcome)
    {
        var pauses = new List<TimeSpan>();
        var observations = new List<DnsCheckObservation>();
        var fault = await Assert.ThrowsAsync<ClientFault>(() => TunnelDnsProbe.CheckAsync(
            (_, _) => throw new InvalidOperationException("Unconfirmed routes must not send DNS."),
            () => new(status, status == DnsRouteStatus.Unavailable ? 1168u : null), observations.Add,
            default, (delay, token) =>
            { token.ThrowIfCancellationRequested(); pauses.Add(delay); return Task.CompletedTask; }));
        Assert.Equal("VPN_DNS_ROUTE_FAILED", fault.Code);
        Assert.InRange(pauses.Count, 1, 15);
        Assert.All(pauses, delay => Assert.InRange(delay.TotalMilliseconds, 1, 500));
        Assert.True(pauses.Sum(delay => delay.TotalMilliseconds) <= 9000);
        Assert.Equal(3, observations.Count);
        var last = observations[^1];
        Assert.All(new[] { last.First, last.Second!, last.Third! }, attempt =>
        { Assert.Equal(outcome, attempt.Outcome); Assert.Equal(status, attempt.Route!.Status); });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CallerCancellationDuringPauseCannotEmitAnObservationOrSend(bool lateCompletion)
    {
        using var cancellation = new CancellationTokenSource();
        var paused = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var released = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var observations = new List<DnsCheckObservation>();
        bool available = false;
        async Task Pause(TimeSpan _, CancellationToken token)
        {
            paused.SetResult();
            if (lateCompletion) { await released.Task; available = true; }
            else await Task.Delay(Timeout.InfiniteTimeSpan, token);
        }
        var check = TunnelDnsProbe.CheckAsync(
            (_, _) => throw new InvalidOperationException("Cancelled readiness must not send DNS."),
            () => new(available ? DnsRouteStatus.OwnedTunnel : DnsRouteStatus.OtherInterface),
            observations.Add, cancellation.Token, Pause);
        await paused.Task.WaitAsync(TimeSpan.FromSeconds(2));
        cancellation.Cancel();
        released.SetResult();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => check.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.Empty(observations);
    }

    [Fact]
    public async Task ARouteArrivingAfterTheAttemptDeadlineNeedsANewAttempt()
    {
        bool available = false;
        int sends = 0, pauses = 0;
        var arrived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var observations = new List<DnsCheckObservation>();
        async Task Pause(TimeSpan _, CancellationToken token)
        {
            if (++pauses > 1)
            {
                await arrived.Task.WaitAsync(token);
                return;
            }
            try { await Task.Delay(Timeout.InfiniteTimeSpan, token).WaitAsync(TimeSpan.FromSeconds(10)); }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            available = true; // A noncooperative wait completes after this attempt was cancelled.
            arrived.SetResult();
        }
        await TunnelDnsProbe.CheckAsync((query, token) =>
        {
            token.ThrowIfCancellationRequested();
            Assert.Single(observations);
            sends++;
            return Task.FromResult(DnsProbeFixtures.Reply(query));
        }, () => new(available ? DnsRouteStatus.OwnedTunnel : DnsRouteStatus.OtherInterface),
            observations.Add, default, Pause);
        Assert.Equal(1, sends);
        Assert.Equal(2, observations.Count);
        Assert.Equal(DnsProbeOutcome.RouteMismatch, observations[^1].First.Outcome);
        Assert.Equal(DnsRouteStatus.OtherInterface, observations[^1].First.Route!.Status);
        Assert.True(observations[^1].Second!.Passed);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FinalFailureCodeUsesTheLastAttemptRatherThanAnEarlierFailure(bool routeFailureFirst)
    {
        bool routeFails = routeFailureFirst;
        var observations = new List<DnsCheckObservation>();
        var fault = await Assert.ThrowsAsync<ClientFault>(() => TunnelDnsProbe.CheckAsync(
            (query, _) => Task.FromResult(DnsProbeFixtures.Reply(query, 2, false)),
            () => new(routeFails ? DnsRouteStatus.OtherInterface : DnsRouteStatus.OwnedTunnel),
            observation => { observations.Add(observation); routeFails = !routeFailureFirst; },
            default, (_, token) => { token.ThrowIfCancellationRequested(); return Task.CompletedTask; }));
        Assert.Equal(3, observations.Count);
        Assert.Equal(routeFailureFirst ? "VPN_DNS_FAILED" : "VPN_DNS_ROUTE_FAILED", fault.Code);
        Assert.Equal(routeFailureFirst ? DnsProbeOutcome.Rejected : DnsProbeOutcome.RouteMismatch,
            observations[^1].Third!.Outcome);
    }
}
