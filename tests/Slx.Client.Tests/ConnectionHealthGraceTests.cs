using Slx.Client.Core;
using Slx.Client.Service;
using Xunit;

namespace Slx.Client.Tests;

public sealed class ConnectionHealthGraceTests
{
    [Fact]
    public async Task RemoteOnlyFailureKeepsSessionCheckingAndLaterSuccessRestoresItsOriginalConnectedTime()
    {
        long now = 0; var grace = new RemoteHealthGrace(() => now, 1); var before = Connected(); var published = new List<TunnelStatus>();
        var deferred = await ConnectionHealthCheck.CheckAsync(before, Failed, () => true, published.Add, default,
            _ => Task.CompletedTask, () => grace.Defer(before.Mode!.Value));
        Assert.True(deferred.Deferred); Assert.False(deferred.Retained); Assert.False(deferred.Confirmation.Passed);
        Assert.All(published, status => { Assert.Equal(TunnelPhase.Checking, status.Phase); Assert.False(status.ProbePassed);
            Assert.Equal(before.ConnectedAt, status.ConnectedAt); Assert.True(status.ProtectionActive); });
        now = 30;
        var retained = await ConnectionHealthCheck.CheckAsync(published[^1], (target, _) => Task.FromResult(new ProbeObservation(target, ProbeOutcome.Success, 1)),
            () => true, published.Add, default, deferFailure: () => throw new InvalidOperationException("Success must not defer"));
        Assert.True(retained.Retained); Assert.False(retained.Deferred); Assert.Equal(TunnelPhase.Connected, published[^1].Phase);
        Assert.Equal(before.ConnectedAt, published[^1].ConnectedAt); Assert.True(published[^1].ProbePassed);
    }
    [Fact]
    public async Task ExpiredSelectedGraceRequiresNormalRecoveryWithoutAnyConnectedPublication()
    {
        long now = 0; var grace = new RemoteHealthGrace(() => now, 1); grace.Defer(RoutingMode.SelectedApplications); now = 90;
        var published = new List<TunnelStatus>();
        var result = await ConnectionHealthCheck.CheckAsync(Connected(), Failed, () => true, published.Add, default,
            _ => Task.CompletedTask, () => grace.Defer(RoutingMode.SelectedApplications));
        Assert.False(result.Deferred); Assert.False(result.Retained); Assert.Equal(TunnelPhase.Reconnecting, published[^1].Phase);
        Assert.DoesNotContain(published, status => status.Phase == TunnelPhase.Connected || status.ProbePassed);
    }
    [Fact]
    public async Task LostOwnedResourcesNeverInvokeGraceEvenIfRemoteEndpointsFail()
    {
        var published = new List<TunnelStatus>();
        var result = await ConnectionHealthCheck.CheckAsync(Connected(), Failed, () => false, published.Add, default,
            _ => Task.CompletedTask, () => throw new InvalidOperationException("Lost resources must recover immediately"));
        Assert.False(result.Deferred); Assert.False(result.Retained); Assert.Equal(TunnelPhase.Reconnecting, published[^1].Phase);
    }
    [Fact]
    public async Task NativeRouteFailureCannotBeConvertedToRemoteGrace()
    {
        var published = new List<TunnelStatus>();
        await Assert.ThrowsAsync<ClientFault>(() => ConnectionHealthCheck.CheckAsync(Connected(), Failed,
            () => throw new ClientFault("VPN_TRAFFIC_ROUTE_FAILED"), published.Add, default, _ => Task.CompletedTask,
            () => throw new InvalidOperationException("A route failure must not defer")));
        Assert.DoesNotContain(published, status => status.ProbePassed);
    }
    [Fact]
    public async Task WholeComputerFailureStillRequiresRecoveryWithoutGrace()
    {
        var published = new List<TunnelStatus>();
        var result = await ConnectionHealthCheck.CheckAsync(Connected() with { Mode = RoutingMode.WholeComputer }, Failed,
            () => throw new InvalidOperationException("Legacy whole-PC failure does not use grace resource lookup"),
            published.Add, default, _ => Task.CompletedTask);
        Assert.False(result.Deferred); Assert.False(result.Retained); Assert.Equal(TunnelPhase.Reconnecting, published[^1].Phase);
    }
    [Fact]
    public async Task ExplicitOffDuringResourceLookupCannotPublishDeferredOrConnected()
    {
        using var cancel = new CancellationTokenSource(); var published = new List<TunnelStatus>();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ConnectionHealthCheck.CheckAsync(Connected(), Failed,
            () => { cancel.Cancel(); return true; }, published.Add, cancel.Token, _ => Task.CompletedTask,
            () => throw new InvalidOperationException("Cancelled lookup cannot authorize grace")));
        Assert.Single(published); Assert.Equal(TunnelPhase.Checking, published[0].Phase); Assert.False(published[0].ProbePassed);
    }
    private static Task<ProbeObservation> Failed(ProbeTarget target, CancellationToken _) => Task.FromResult(new ProbeObservation(target, ProbeOutcome.Timeout, 1));
    private static TunnelStatus Connected() => new(TunnelPhase.Connected, DateTimeOffset.UtcNow, "Fixture",
        ConnectedAt: DateTimeOffset.UtcNow.AddHours(-1), Mode: RoutingMode.SelectedApplications, ProbePassed: true, ProtectionActive: true);
}
