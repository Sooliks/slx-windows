using Slx.Client.Core;
using Slx.Client.Service;
using Xunit;

namespace Slx.Client.Tests;

public sealed class ScopedRecoveryAttemptTests
{
    private static readonly CoreRecoveryResources HealthyFront = new(true, true, true, true);

    [Theory]
    [InlineData("VPN_PROBE_FAILED")]
    [InlineData("CORE_EXITED")]
    [InlineData("CORE_START_FAILED")]
    [InlineData("CORE_STOP_FAILED")]
    [InlineData("CORE_CONTAINMENT_FAILED")]
    public async Task FailedVpnCoreNeverUsesFullTunnelCleanup(string code)
    {
        var policy = new CoreRecoveryPolicy();
        int coreCleanup = 0, fullWork = 0, fullCleanup = 0;
        var events = new List<TunnelLifecycleOutcome>();
        var result = await RecoveryAttempt.RunAsync(policy.Choose(HealthyFront, CoreRecoveryPolicy.ReasonFor(code)),
            () => "verified-runtime", (_, _) => Task.FromException(new ClientFault(code)),
            (_, _) => { fullWork++; return Task.CompletedTask; },
            observed => { Assert.Equal(code, observed); coreCleanup++; return Task.CompletedTask; },
            _ => { fullCleanup++; return Task.CompletedTask; }, (outcome, _) => events.Add(outcome), default);
        Assert.False(result.Succeeded); Assert.Equal(code, result.Code);
        Assert.Equal(1, coreCleanup); Assert.Equal(0, fullWork); Assert.Equal(0, fullCleanup);
        Assert.Equal([TunnelLifecycleOutcome.Started, TunnelLifecycleOutcome.Failed], events);
        Assert.Equal(TunnelLifecycleScope.VpnCore, policy.Choose(HealthyFront, CoreRecoveryPolicy.ReasonFor(code)).Scope);
    }

    [Fact]
    public async Task VerificationFailureBeforeCoreStartRetainsTheSameCleanupScope()
    {
        int coreWork = 0, coreCleanup = 0, fullCleanup = 0;
        var result = await RecoveryAttempt.RunAsync(new(TunnelLifecycleScope.VpnCore, TunnelLifecycleReason.HealthProbeFailed),
            () => throw new ClientFault("RUNTIME_HASH_MISMATCH"),
            (_, _) => { coreWork++; return Task.CompletedTask; }, (_, _) => throw new InvalidOperationException(),
            _ => { coreCleanup++; return Task.CompletedTask; }, _ => { fullCleanup++; return Task.CompletedTask; }, (_, _) => { }, default);
        Assert.Equal("RUNTIME_HASH_MISMATCH", result.Code);
        Assert.Equal(0, coreWork); Assert.Equal(1, coreCleanup); Assert.Equal(0, fullCleanup);
    }

    [Theory]
    [InlineData(TunnelLifecycleReason.ProtectionBaselineInvalid)]
    [InlineData(TunnelLifecycleReason.TrafficRouteFailed)]
    public async Task FailedRequiredFullRecoveryCannotFallBackToAHealthyLookingCore(TunnelLifecycleReason reason)
    {
        var policy = new CoreRecoveryPolicy(); policy.RequireFull(reason);
        int fullWork = 0, fullCleanup = 0;
        var result = await RecoveryAttempt.RunAsync(policy.Choose(HealthyFront, TunnelLifecycleReason.HealthProbeFailed),
            () => "verified-runtime", (_, _) => throw new InvalidOperationException("Core-only is unsafe."),
            (_, _) => { fullWork++; return Task.FromException(new ClientFault("TUN_NOT_READY")); },
            _ => throw new InvalidOperationException("Full scope requires full cleanup."),
            _ => { fullCleanup++; return Task.CompletedTask; }, (_, _) => { }, default);
        Assert.Equal("TUN_NOT_READY", result.Code); Assert.Equal(1, fullWork); Assert.Equal(1, fullCleanup);
        Assert.Equal(reason, policy.RequiredFullReason);
    }

    [Fact]
    public async Task ExplicitOffCancelsScopedWorkWithoutConnectedOrAutomaticTeardown()
    {
        var work = new TunnelWork();
        using var running = work.Begin(default);
        var waiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var events = new List<TunnelLifecycleOutcome>();
        int cleanup = 0;
        var attempt = RecoveryAttempt.RunAsync(new(TunnelLifecycleScope.VpnCore, TunnelLifecycleReason.HealthProbeFailed),
            () => "verified-runtime", async (_, token) => { waiting.SetResult(); await Task.Delay(Timeout.Infinite, token); },
            (_, _) => throw new InvalidOperationException(), _ => { cleanup++; return Task.CompletedTask; },
            _ => { cleanup++; return Task.CompletedTask; }, (outcome, _) => events.Add(outcome), running.Token);
        await waiting.Task;
        using (work.RequestStop()) await Assert.ThrowsAnyAsync<OperationCanceledException>(() => attempt);
        Assert.Equal(0, cleanup);
        Assert.Equal([TunnelLifecycleOutcome.Started, TunnelLifecycleOutcome.Cancelled], events);
    }

    [Fact]
    public async Task SuccessRecordsOnlyTheScopeThatActuallyRan()
    {
        int coreWork = 0;
        var events = new List<TunnelLifecycleOutcome>();
        var result = await RecoveryAttempt.RunAsync(new(TunnelLifecycleScope.VpnCore, TunnelLifecycleReason.CoreExited),
            () => "verified-runtime", (runtime, _) => { Assert.Equal("verified-runtime", runtime); coreWork++; return Task.CompletedTask; },
            (_, _) => throw new InvalidOperationException(), _ => throw new InvalidOperationException(),
            _ => throw new InvalidOperationException(), (outcome, _) => events.Add(outcome), default);
        Assert.True(result.Succeeded); Assert.Null(result.Code); Assert.Equal(1, coreWork);
        Assert.Equal([TunnelLifecycleOutcome.Started, TunnelLifecycleOutcome.Connected], events);
    }
}
