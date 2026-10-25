using Slx.Client.Core;
using Slx.Client.Service;
using Xunit;

namespace Slx.Client.Tests;

public sealed class TrafficRouteLifecycleTests
{
    [Fact]
    public async Task SuccessfulProxyHealthCannotRetainAConnectionWithForeignOrdinaryDataRoutes()
    {
        var published = new List<TunnelStatus>();
        int proxyRequests = 0;
        var connected = new TunnelStatus(TunnelPhase.Connected, DateTimeOffset.UtcNow,
            ConnectedAt: DateTimeOffset.UtcNow.AddMinutes(-3), ProbePassed: true, ProtectionActive: true);
        var fault = await Assert.ThrowsAsync<ClientFault>(() => ConnectionHealthCheck.CheckAsync(connected,
            (target, _) =>
            { proxyRequests++; return Task.FromResult(new ProbeObservation(target, ProbeOutcome.Success, 1)); },
            () => { TrafficRouteProbe.RequireReady(TrafficRouteProbeTests.Snapshot(true)); return true; },
            published.Add, default));
        Assert.Equal(TrafficRouteProbe.FailureCode, fault.Code);
        Assert.Equal(1, proxyRequests);
        Assert.DoesNotContain(published, status => status.Phase == TunnelPhase.Connected && status.ProbePassed);
    }

    [Fact]
    public async Task SuccessfulProxyAndDnsDoNotBypassDataRouteReadinessOrItsCompletedInitialRollback()
    {
        bool proxyPassed = true, dnsPassed = true, connected = false;
        var rolledBack = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var attempt = InitialConnectionAttempt.RunAsync(true, async () =>
        {
            Assert.True(proxyPassed && dnsPassed);
            await TrafficRouteProbe.CheckAsync(_ => TrafficRouteProbeTests.Snapshot(true), _ => { }, default,
                (_, _) => Task.CompletedTask);
            connected = true;
        }, async code =>
        { Assert.Equal(TrafficRouteProbe.FailureCode, code); rolledBack.SetResult(); await release.Task; }, default);
        await rolledBack.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.False(attempt.IsCompleted); Assert.False(connected);
        release.SetResult();
        Assert.Equal(TrafficRouteProbe.FailureCode, (await Assert.ThrowsAsync<ClientFault>(() => attempt)).Code);
    }

    [Fact]
    public async Task EstablishedRouteFailurePreservesProtectionInsteadOfRollingBackAnExistingSession()
    {
        var fault = await Assert.ThrowsAsync<ClientFault>(() => InitialConnectionAttempt.RunAsync(false,
            () => throw new ClientFault(TrafficRouteProbe.FailureCode),
            _ => throw new InvalidOperationException("Established protection cannot be released."), default));
        Assert.Equal(TrafficRouteProbe.FailureCode, fault.Code);
    }
}
