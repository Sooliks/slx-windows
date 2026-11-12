using Slx.Client.Core;
using Slx.Client.Service;
using Xunit;

namespace Slx.Client.Tests;

public sealed class ConnectionHealthGraceTests
{
    [Fact]
    public async Task AnExistingStreamKeepsSessionCheckingAndFreshSuccessRestoresItsOriginalConnectedTime()
    {
        var grace = new UpstreamStallHold(); var before = Connected(); var published = new List<TunnelStatus>();
        var deferred = await ConnectionHealthCheck.CheckAsync(before, Failed, () => true, published.Add, default,
            _ => Task.CompletedTask, grace.Hold, ExistingStream);
        Assert.True(deferred.Deferred); Assert.False(deferred.Retained); Assert.False(deferred.Confirmation.Passed);
        Assert.All(published, status => { Assert.Equal(TunnelPhase.Checking, status.Phase); Assert.False(status.ProbePassed);
            Assert.Equal(before.ConnectedAt, status.ConnectedAt); Assert.True(status.ProtectionActive); });
        var retained = await ConnectionHealthCheck.CheckAsync(published[^1], (target, _) => Task.FromResult(new ProbeObservation(target, ProbeOutcome.Success, 1)),
            () => true, published.Add, default, deferFailure: () => throw new InvalidOperationException("Success must not defer"));
        Assert.True(retained.Retained); Assert.False(retained.Deferred); Assert.Equal(TunnelPhase.Connected, published[^1].Phase);
        Assert.Equal(before.ConnectedAt, published[^1].ConnectedAt); Assert.True(published[^1].ProbePassed);
    }
    [Fact]
    public async Task FreshAlternateSuccessClearsCheckingEvenWhenTheExistingStreamResponds()
    {
        var published = new List<TunnelStatus>();
        var connected = Connected();
        var result = await ConnectionHealthCheck.CheckAsync(connected,
            (target, token) => target == ProbeTarget.Slx
                ? Task.FromResult(new ProbeObservation(target, ProbeOutcome.Success, 1, 200))
                : Failed(target, token),
            () => true, published.Add, default, _ => Task.CompletedTask,
            () => throw new InvalidOperationException("Fresh alternate success must not defer"), ExistingStream);

        Assert.True(result.Retained);
        Assert.False(result.Deferred);
        Assert.False(result.Confirmation.OnlyEstablishedStreamPassed);
        Assert.Equal(TunnelPhase.Connected, published[^1].Phase);
        Assert.Equal(connected.ConnectedAt, published[^1].ConnectedAt);
    }
    [Fact]
    public async Task ExpiredWindowRequiresNormalRecoveryWithoutAnyConnectedPublication()
    {
        long now = 0; var grace = new UpstreamStallHold(() => now, 1); Assert.True(grace.Hold()); now = 45;
        var published = new List<TunnelStatus>();
        var result = await ConnectionHealthCheck.CheckAsync(Connected(), Failed, () => true, published.Add, default,
            _ => Task.CompletedTask, grace.Hold, ExistingStream);
        Assert.False(result.Deferred); Assert.False(result.Retained); Assert.Equal(TunnelPhase.Reconnecting, published[^1].Phase);
        Assert.DoesNotContain(published, status => status.Phase == TunnelPhase.Connected || status.ProbePassed);
    }
    [Fact]
    public async Task LostOwnedResourcesNeverInvokeGraceEvenIfRemoteEndpointsFail()
    {
        var published = new List<TunnelStatus>();
        var result = await ConnectionHealthCheck.CheckAsync(Connected(), Failed, () => false, published.Add, default,
            _ => Task.CompletedTask, () => throw new InvalidOperationException("Lost resources must recover immediately"), ExistingStream);
        Assert.False(result.Deferred); Assert.False(result.Retained); Assert.Equal(TunnelPhase.Reconnecting, published[^1].Phase);
    }
    [Fact]
    public async Task NativeRouteFailureCannotBeConvertedToRemoteGrace()
    {
        var published = new List<TunnelStatus>();
        await Assert.ThrowsAsync<ClientFault>(() => ConnectionHealthCheck.CheckAsync(Connected(), Failed,
            () => throw new ClientFault("VPN_TRAFFIC_ROUTE_FAILED"), published.Add, default, _ => Task.CompletedTask,
            () => throw new InvalidOperationException("A route failure must not defer"), ExistingStream));
        Assert.DoesNotContain(published, status => status.ProbePassed);
    }
    [Fact]
    public async Task WholeComputerFailureWithNoExistingEvidenceRequiresRecovery()
    {
        var published = new List<TunnelStatus>();
        var result = await ConnectionHealthCheck.CheckAsync(Connected() with { Mode = RoutingMode.WholeComputer }, Failed,
            () => throw new InvalidOperationException("No existing evidence to validate for deferral"),
            published.Add, default, _ => Task.CompletedTask, () => throw new InvalidOperationException("No evidence"));
        Assert.False(result.Deferred); Assert.False(result.Retained); Assert.Equal(TunnelPhase.Reconnecting, published[^1].Phase);
    }
    [Fact]
    public async Task ExplicitOffDuringResourceLookupCannotPublishDeferredOrConnected()
    {
        using var cancel = new CancellationTokenSource(); var published = new List<TunnelStatus>();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ConnectionHealthCheck.CheckAsync(Connected(), Failed,
            () => { cancel.Cancel(); return true; }, published.Add, cancel.Token, _ => Task.CompletedTask,
            () => throw new InvalidOperationException("Cancelled lookup cannot authorize grace"), ExistingStream));
        Assert.Single(published); Assert.Equal(TunnelPhase.Checking, published[0].Phase); Assert.False(published[0].ProbePassed);
    }
    private static Task<ProbeObservation> Failed(ProbeTarget target, CancellationToken _) => Task.FromResult(new ProbeObservation(target, ProbeOutcome.Timeout, 1));
    private static Task<EstablishedObservation> ExistingStream(CancellationToken _) =>
        Task.FromResult(new EstablishedObservation(new(ProbeTarget.Gstatic, ProbeOutcome.Success, 1, 204), Reused: true));
    private static TunnelStatus Connected() => new(TunnelPhase.Connected, DateTimeOffset.UtcNow, "Fixture",
        ConnectedAt: DateTimeOffset.UtcNow.AddHours(-1), Mode: RoutingMode.SelectedApplications, ProbePassed: true, ProtectionActive: true);
}
