using Slx.Client.Core;
using Slx.Client.Service;
using Xunit;

namespace Slx.Client.Tests;

public sealed class TunnelRecoveryPolicyTests
{
    [Theory]
    [InlineData(TunnelPhase.Disconnected, false, false, false, false, true)]
    [InlineData(TunnelPhase.Faulted, false, false, false, false, true)]
    [InlineData(TunnelPhase.Disconnected, true, false, false, false, false)]
    [InlineData(TunnelPhase.Connected, true, true, true, true, false)]
    [InlineData(TunnelPhase.Reconnecting, true, true, true, false, false)]
    [InlineData(TunnelPhase.Disconnected, false, true, false, false, false)]
    [InlineData(TunnelPhase.Disconnected, false, false, true, false, false)]
    [InlineData(TunnelPhase.Disconnected, false, false, false, true, false)]
    public void OnlyANewUnprotectedAttemptCanBeRolledBack(TunnelPhase phase, bool protection,
        bool desired, bool plan, bool resources, bool expected)
    {
        var status = new TunnelStatus(phase, DateTimeOffset.UtcNow, ProtectionActive: protection);
        Assert.Equal(expected, InitialConnectionAttempt.CanRollback(status, desired, plan, resources));
    }

    [Fact]
    public void UnknownOrCancelledOutcomeNeverAuthorizesProtectionRelease()
    {
        Assert.True(InitialConnectionAttempt.IsDefinitive(new ClientFault("VPN_PROBE_FAILED"), default));
        Assert.False(InitialConnectionAttempt.IsDefinitive(new OperationCanceledException(), default));
        Assert.False(InitialConnectionAttempt.IsDefinitive(new ClientFault("OPERATION_INTERRUPTED"), default));
        Assert.False(InitialConnectionAttempt.IsDefinitive(new IOException(), default));
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        Assert.False(InitialConnectionAttempt.IsDefinitive(new ClientFault("VPN_PROBE_FAILED"), cancelled.Token));
    }

    [Fact]
    public async Task FailedOffRetriesCompleteCleanupWithPacingAndRetainsTheStartupError()
    {
        long now = 0;
        int calls = 0;
        var off = new PendingOffCleanup(() => now, 1000);
        Assert.False(await off.TryAsync(() => throw new InvalidOperationException()));
        off.Begin("VPN_PROBE_FAILED");
        Task Failing() { calls++; throw new ClientFault("TUN_CLEANUP_PENDING"); }
        await Assert.ThrowsAsync<ClientFault>(() => off.TryAsync(Failing));
        Assert.True(off.Pending);
        now = 1999;
        Assert.False(await off.TryAsync(Failing));
        Assert.Equal(1, calls);
        now = 2000;
        Assert.True(await off.TryAsync(() => { calls++; return Task.CompletedTask; }));
        Assert.False(off.Pending);
        Assert.Equal("VPN_PROBE_FAILED", off.CompletionCode);
        now = 100000;
        Assert.False(await off.TryAsync(Failing));
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task ExplicitOffCanRetryPendingCleanupWithoutWaitingForItsBackoff()
    {
        var off = new PendingOffCleanup(() => 0, 1000);
        off.Begin("VPN_DNS_FAILED");
        await Assert.ThrowsAsync<ClientFault>(() => off.TryAsync(() => throw new ClientFault("CORE_STOP_FAILED")));
        off.Begin();
        Assert.True(await off.TryAsync(() => Task.CompletedTask, force: true));
        Assert.Null(off.CompletionCode);
    }

    [Fact]
    public void ExplicitStopInterruptsHealthAndPreventsAnotherReadinessWaitUntilOffFinishes()
    {
        var work = new TunnelWork();
        using var first = work.Begin(default);
        using (work.RequestStop())
        {
            Assert.True(first.Token.IsCancellationRequested);
            using var raced = work.Begin(default);
            Assert.True(raced.Token.IsCancellationRequested);
        }
        using var later = work.Begin(default);
        Assert.False(later.Token.IsCancellationRequested);
    }
}
