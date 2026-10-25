using System.Net;
using Slx.Client.Core;
using Slx.Client.Service;
using Xunit;

namespace Slx.Client.Tests;

public sealed class TrafficRouteProbeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FixedDataSamplesAreIndependentOfTheDnsHostRoute(bool ipv6)
    {
        var reads = new List<string>();
        var result = TrafficRouteProbe.Observe(address =>
        { reads.Add(address.ToString()); return Owned(); }, () => ipv6, default);
        Assert.Equal(ipv6 ? new[] { "8.8.8.8", "142.250.0.1", "2001:4860:4860::8888" }
            : ["8.8.8.8", "142.250.0.1"], reads);
        Assert.DoesNotContain("1.1.1.1", reads);
        Assert.Equal(ipv6, result.Ipv6 is not null);
        Assert.True(TrafficRouteProbe.IsReady(result));
    }

    [Theory]
    [InlineData(0, DnsRouteStatus.OtherInterface)]
    [InlineData(1, DnsRouteStatus.OtherInterface)]
    [InlineData(2, DnsRouteStatus.OtherInterface)]
    [InlineData(0, DnsRouteStatus.Unavailable)]
    [InlineData(1, DnsRouteStatus.Unavailable)]
    [InlineData(2, DnsRouteStatus.Unavailable)]
    public void EveryRequiredSampleMustBelongToTheOwnedTunnel(int failed, DnsRouteStatus status)
    {
        var entries = new[] { Owned(), Owned(), Owned() };
        entries[failed] = new(status);
        var result = new TrafficRouteCheckObservation(DateTimeOffset.UtcNow, entries[0], entries[1], entries[2]);
        Assert.False(TrafficRouteProbe.IsReady(result));
        Assert.Equal(TrafficRouteProbe.FailureCode, Assert.Throws<ClientFault>(() => TrafficRouteProbe.RequireReady(result)).Code);
    }

    [Fact]
    public async Task TransientRouteConvergenceWaitsWithoutSendingPackets()
    {
        int reads = 0, pauses = 0;
        var observations = new List<TrafficRouteCheckObservation>();
        await TrafficRouteProbe.CheckAsync(_ => Snapshot(++reads < 3), observations.Add, default,
            (delay, token) =>
            { token.ThrowIfCancellationRequested(); Assert.Equal(TimeSpan.FromMilliseconds(500), delay); pauses++; return Task.CompletedTask; });
        Assert.Equal(3, reads); Assert.Equal(2, pauses); Assert.Equal(3, observations.Count);
        Assert.False(TrafficRouteProbe.IsReady(observations[0]));
        Assert.True(TrafficRouteProbe.IsReady(observations[^1]));
    }

    [Fact]
    public async Task PersistentConflictHasAtMostSixReadsAndFivePauses()
    {
        int reads = 0, pauses = 0;
        var fault = await Assert.ThrowsAsync<ClientFault>(() => TrafficRouteProbe.CheckAsync(
            _ => { reads++; return Snapshot(true); }, _ => { }, default,
            (_, _) => { pauses++; return Task.CompletedTask; }));
        Assert.Equal(TrafficRouteProbe.FailureCode, fault.Code);
        Assert.Equal(6, reads); Assert.Equal(5, pauses);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationDuringReadOrPublicationCannotReturnSuccess(bool duringPublication)
    {
        using var cancel = new CancellationTokenSource();
        int observations = 0;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => TrafficRouteProbe.CheckAsync(_ =>
        { if (!duringPublication) cancel.Cancel(); return Snapshot(false); }, _ =>
        { observations++; if (duringPublication) cancel.Cancel(); }, cancel.Token));
        Assert.Equal(duringPublication ? 1 : 0, observations);
    }

    [Fact]
    public async Task CallerCancellationInterruptsANoncooperativePause()
    {
        using var cancel = new CancellationTokenSource();
        var paused = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var never = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var check = TrafficRouteProbe.CheckAsync(_ => Snapshot(true), _ => { }, cancel.Token,
            (_, _) => { paused.SetResult(); return never.Task; });
        await paused.Task.WaitAsync(TimeSpan.FromSeconds(2));
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => check.WaitAsync(TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public async Task IndependentThreeSecondDeadlineRejectsAnUnboundedWait()
    {
        int reads = 0;
        var fault = await Assert.ThrowsAsync<ClientFault>(() => TrafficRouteProbe.CheckAsync(
            _ => { reads++; return Snapshot(true); }, _ => { }, default,
            (_, _) => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously).Task)
            .WaitAsync(TimeSpan.FromSeconds(8)));
        Assert.Equal(TrafficRouteProbe.FailureCode, fault.Code); Assert.Equal(1, reads);
    }

    [Fact]
    public async Task LateNativeSuccessAfterTheDeadlineCannotOverwriteTheLastObservation()
    {
        var observations = new List<TrafficRouteCheckObservation>();
        var fault = await Assert.ThrowsAsync<ClientFault>(() => TrafficRouteProbe.CheckAsync(token =>
        {
            try { Task.Delay(Timeout.Infinite, token).GetAwaiter().GetResult(); }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            return Snapshot(false);
        }, observations.Add, default).WaitAsync(TimeSpan.FromSeconds(8)));
        Assert.Equal(TrafficRouteProbe.FailureCode, fault.Code);
        Assert.Empty(observations);
    }

    [Fact]
    public async Task APreCancelledRequestDoesNotReadOrPublishRoutes()
    {
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => TrafficRouteProbe.CheckAsync(
            _ => throw new InvalidOperationException("Cancelled work cannot read routes."),
            _ => throw new InvalidOperationException("Cancelled work cannot publish routes."), cancel.Token));
    }

    [Fact]
    public void CancellationInsideTheIpv6EligibilityReadCannotProduceAFreshSnapshot()
    {
        using var cancel = new CancellationTokenSource();
        int reads = 0;
        Assert.ThrowsAny<OperationCanceledException>(() => TrafficRouteProbe.Observe(_ =>
            { reads++; return Owned(); }, () => { cancel.Cancel(); return true; }, cancel.Token));
        Assert.Equal(2, reads);
    }

    internal static DnsRouteObservation Owned() => new(DnsRouteStatus.OwnedTunnel, PrefixLength: 1, RouteMetric: 0);
    internal static TrafficRouteCheckObservation Snapshot(bool conflict) => new(DateTimeOffset.UtcNow,
        Owned(), conflict ? new(DnsRouteStatus.OtherInterface, PrefixLength: 2, RouteMetric: 0) : Owned());
}
