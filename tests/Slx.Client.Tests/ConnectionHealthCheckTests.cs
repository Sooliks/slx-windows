using Slx.Client.Core;
using Slx.Client.Service;
using Xunit;
namespace Slx.Client.Tests;

public sealed class ConnectionHealthCheckTests
{
    [Fact]
    public async Task PrimarySuccessRetainsTheEstablishedSessionWithoutAPendingPhase()
    {
        var before = Connected();
        var published = new List<TunnelStatus>();
        int calls = 0;
        var result = await ConnectionHealthCheck.CheckAsync(before, (target, _) =>
        { calls++; return Task.FromResult(Observation(target, true)); }, () => true, published.Add, default);
        Assert.True(result.Retained); Assert.Equal(1, calls);
        var after = Assert.Single(published);
        Assert.Equal(TunnelPhase.Connected, after.Phase); Assert.True(after.ProbePassed);
        AssertSessionPreserved(before, after);
    }

    [Theory]
    [InlineData(ProbeTarget.Gstatic)]
    [InlineData(ProbeTarget.Slx)]
    public async Task PendingConfirmationReportsCheckingThenRetainsTheSameSession(ProbeTarget successful)
    {
        var before = Connected();
        var published = new List<TunnelStatus>();
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls = 0;
        var checking = ConnectionHealthCheck.CheckAsync(before, (target, _) =>
            Task.FromResult(Observation(target, ++calls > 1 && target == successful)), () => true,
            published.Add, default, _ => resume.Task);
        Assert.False(checking.IsCompleted);
        var pending = Assert.Single(published);
        Assert.Equal(TunnelPhase.Checking, pending.Phase); Assert.False(pending.ProbePassed);
        AssertSessionPreserved(before, pending);
        resume.SetResult();
        var result = await checking;
        Assert.True(result.Retained); Assert.Equal(3, calls);
        Assert.Equal(new[] { TunnelPhase.Checking, TunnelPhase.Connected }, published.Select(x => x.Phase));
        Assert.True(published[^1].ProbePassed); AssertSessionPreserved(before, published[^1]);
    }

    [Fact]
    public async Task FailedConfirmationRequestsRecoveryOnlyAfterItsResultsAreKnown()
    {
        var before = Connected();
        var published = new List<TunnelStatus>();
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = new List<ProbeTarget>();
        var checking = ConnectionHealthCheck.CheckAsync(before, (target, _) =>
        { calls.Add(target); return Task.FromResult(Observation(target, false)); },
            () => throw new InvalidOperationException("Failed probes cannot retain the session."),
            published.Add, default, _ => resume.Task);
        Assert.Equal(TunnelPhase.Checking, Assert.Single(published).Phase);
        resume.SetResult();
        var result = await checking;
        Assert.False(result.Retained); Assert.False(result.Confirmation.Passed);
        Assert.Equal(new[] { ProbeTarget.Gstatic, ProbeTarget.Gstatic, ProbeTarget.Slx }, calls);
        Assert.Equal(new[] { TunnelPhase.Checking, TunnelPhase.Reconnecting }, published.Select(x => x.Phase));
        Assert.False(published[^1].ProbePassed); AssertSessionPreserved(before, published[^1]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LostOwnedResourcesRequireRecoveryEvenWhenAProbeSucceeds(bool failedPrimary)
    {
        var published = new List<TunnelStatus>();
        int calls = 0;
        var result = await ConnectionHealthCheck.CheckAsync(Connected(), (target, _) =>
            Task.FromResult(Observation(target, ++calls > 1 || !failedPrimary)), () => false,
            published.Add, default, _ => Task.CompletedTask);
        Assert.True(result.Confirmation.Passed); Assert.False(result.Retained);
        Assert.Equal(failedPrimary ? 2 : 1, published.Count);
        Assert.Equal(TunnelPhase.Reconnecting, published[^1].Phase);
        Assert.False(published[^1].ProbePassed); Assert.True(published[^1].ProtectionActive);
    }

    [Fact]
    public async Task ExplicitOffInterruptsCheckingWithoutPublishingSuccessOrStartingRecovery()
    {
        var work = new TunnelWork();
        using var operation = work.Begin(default);
        var before = Connected();
        var published = new List<TunnelStatus>();
        var checking = ConnectionHealthCheck.CheckAsync(before,
            (target, _) => Task.FromResult(Observation(target, false)),
            () => throw new InvalidOperationException("Cancelled checks cannot retain the session."),
            published.Add, operation.Token, token => Task.Delay(Timeout.Infinite, token));
        Assert.Equal(TunnelPhase.Checking, Assert.Single(published).Phase);
        using (work.RequestStop()) await Assert.ThrowsAnyAsync<OperationCanceledException>(() => checking);
        var pending = Assert.Single(published);
        Assert.False(pending.ProbePassed); AssertSessionPreserved(before, pending);
    }

    [Fact]
    public async Task CancellationRacingSuccessfulConfirmationCannotPublishConnected()
    {
        using var cancel = new CancellationTokenSource();
        var published = new List<TunnelStatus>();
        int calls = 0;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ConnectionHealthCheck.CheckAsync(Connected(),
            (target, _) =>
            {
                calls++;
                if (calls == 2) cancel.Cancel();
                return Task.FromResult(Observation(target, calls > 1));
            }, () => true, published.Add, cancel.Token, _ => Task.CompletedTask));
        var pending = Assert.Single(published);
        Assert.Equal(TunnelPhase.Checking, pending.Phase); Assert.False(pending.ProbePassed);
        Assert.True(pending.ProtectionActive);
    }

    [Fact]
    public async Task ExplicitOffDuringOwnedResourceLookupCannotPublishConnected()
    {
        using var cancel = new CancellationTokenSource();
        var published = new List<TunnelStatus>();
        int calls = 0, resourceReads = 0;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ConnectionHealthCheck.CheckAsync(Connected(),
            (target, _) => Task.FromResult(Observation(target, ++calls > 1)),
            () => { resourceReads++; cancel.Cancel(); return true; },
            published.Add, cancel.Token, _ => Task.CompletedTask));
        Assert.Equal(1, resourceReads);
        var pending = Assert.Single(published);
        Assert.Equal(TunnelPhase.Checking, pending.Phase); Assert.False(pending.ProbePassed);
        Assert.True(pending.ProtectionActive);
    }

    private static ProbeObservation Observation(ProbeTarget target, bool passed) =>
        new(target, passed ? ProbeOutcome.Success : ProbeOutcome.Timeout, 1);
    private static TunnelStatus Connected() => new(TunnelPhase.Connected, DateTimeOffset.UtcNow,
        "Fixture", ConnectedAt: DateTimeOffset.UtcNow.AddMinutes(-10), Mode: RoutingMode.SelectedApplications,
        ProbePassed: true, ProtectionActive: true, ApplicationRevision: "confirmed-fixture-revision");
    private static void AssertSessionPreserved(TunnelStatus before, TunnelStatus after)
    {
        Assert.Equal(before.ConnectedAt, after.ConnectedAt); Assert.Equal(before.ProfileName, after.ProfileName);
        Assert.Equal(before.Mode, after.Mode); Assert.Equal(before.ApplicationRevision, after.ApplicationRevision);
        Assert.True(after.ProtectionActive); Assert.Equal(before.Code, after.Code);
        Assert.Null(after.Recovery); Assert.True(after.ObservedAt >= before.ObservedAt);
    }
}
