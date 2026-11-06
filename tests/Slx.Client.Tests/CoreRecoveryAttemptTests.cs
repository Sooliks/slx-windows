using Slx.Client.Core;
using Slx.Client.Service;
using Xunit;

namespace Slx.Client.Tests;

public sealed class CoreRecoveryAttemptTests
{
    [Fact]
    public void CoreOnlySuccessDoesNotConfirmAnUnacknowledgedFrontRoutingChange()
    {
        var plan = CoreFixtures.Plan();
        var pending = CoreRecoveryAttempt.ConnectedStatus(plan, true, applicationUpdatePending: true);
        Assert.Equal(TunnelPhase.Connected, pending.Phase); Assert.True(pending.ProbePassed); Assert.True(pending.ProtectionActive);
        Assert.Null(pending.ApplicationRevision); Assert.Equal("ROUTING_UPDATE_UNCONFIRMED", pending.Code);
        var confirmed = CoreRecoveryAttempt.ConnectedStatus(plan, true, applicationUpdatePending: false);
        Assert.Equal(XrayRouteRevision.For(plan), confirmed.ApplicationRevision); Assert.Null(confirmed.Code);
    }

    [Fact]
    public async Task RemoteSuccessRequiresAFreshRouteCheckBeforeConnected()
    {
        var calls = new List<string>();
        Task Step(string name) { calls.Add(name); return Task.CompletedTask; }
        await CoreRecoveryAttempt.RunAsync(() => Step("stop"), _ => Step("start"), _ => Step("vpn"), _ => Step("routes"),
            () => calls.Add("invalid"), () => { calls.Add("resources"); return true; }, () => calls.Add("connected"), default);
        Assert.Equal(["stop", "start", "vpn", "routes", "resources", "connected"], calls);
    }

    [Fact]
    public async Task WorkingHttpsWithForeignRoutesRequiresFullRecoveryAndCannotPublishConnected()
    {
        var policy = new CoreRecoveryPolicy();
        bool connected = false, resourcesRead = false;
        var fault = await Assert.ThrowsAsync<ClientFault>(() => CoreRecoveryAttempt.RunAsync(Done, _ => Done(), _ => Done(),
            _ => Task.FromException(new ClientFault(TrafficRouteProbe.FailureCode)),
            () => policy.RequireFull(TunnelLifecycleReason.TrafficRouteFailed),
            () => { resourcesRead = true; return true; }, () => connected = true, default));
        Assert.Equal(TrafficRouteProbe.FailureCode, fault.Code);
        Assert.False(connected); Assert.False(resourcesRead);
        Assert.Equal(TunnelLifecycleScope.FullTunnel,
            policy.Choose(new(true, true, true, true), TunnelLifecycleReason.HealthProbeFailed).Scope);
    }

    [Theory]
    [InlineData("routes")]
    [InlineData("resources")]
    public async Task ExplicitOffDuringFinalReadinessCannotPublishConnected(string point)
    {
        using var stop = new CancellationTokenSource();
        bool connected = false, invalidated = false;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CoreRecoveryAttempt.RunAsync(Done, _ => Done(), _ => Done(),
            _ => { if (point == "routes") stop.Cancel(); return Done(); }, () => invalidated = true,
            () => { if (point == "resources") stop.Cancel(); return true; }, () => connected = true, stop.Token));
        Assert.False(connected); Assert.False(invalidated);
    }

    [Fact]
    public async Task CancellationOfRouteObservationDoesNotInventAForeignRoute()
    {
        using var stop = new CancellationTokenSource();
        var policy = new CoreRecoveryPolicy();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CoreRecoveryAttempt.RunAsync(Done, _ => Done(), _ => Done(),
            _ => { stop.Cancel(); return Task.FromException(new ClientFault(TrafficRouteProbe.FailureCode)); },
            () => policy.RequireFull(TunnelLifecycleReason.TrafficRouteFailed), () => true,
            () => throw new InvalidOperationException("Cancelled work cannot connect."), stop.Token));
        Assert.Null(policy.RequiredFullReason);
    }

    [Fact]
    public async Task AResourceThatDisappearedAfterRoutesWereReadCannotPublishConnected()
    {
        bool connected = false;
        var fault = await Assert.ThrowsAsync<ClientFault>(() => CoreRecoveryAttempt.RunAsync(Done, _ => Done(), _ => Done(),
            _ => Done(), () => throw new InvalidOperationException("Routes passed."), () => false,
            () => connected = true, default));
        Assert.Equal("CORE_EXITED", fault.Code); Assert.False(connected);
    }

    [Fact]
    public async Task CancellationBeforeRecoveryDoesNotStopAnyProcess()
    {
        using var stop = new CancellationTokenSource(); stop.Cancel();
        int calls = 0;
        Task Count() { calls++; return Done(); }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CoreRecoveryAttempt.RunAsync(Count, _ => Count(), _ => Count(),
            _ => Count(), () => calls++, () => { calls++; return true; }, () => calls++, stop.Token));
        Assert.Equal(0, calls);
    }

    private static Task Done() => Task.CompletedTask;
}
