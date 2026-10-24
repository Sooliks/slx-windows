using Slx.Client.Core;
using Slx.Client.Service;
using Xunit;

namespace Slx.Client.Tests;

public sealed class InitialConnectionAttemptTests
{
    [Fact]
    public async Task FailedInitialConnectAwaitsRollbackBeforeReportingTheOriginalFailure()
    {
        var released = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var rollbackStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var attempt = InitialConnectionAttempt.RunAsync(true, () => throw new ClientFault("VPN_PROBE_FAILED"), async code =>
        {
            Assert.Equal("VPN_PROBE_FAILED", code);
            rollbackStarted.SetResult();
            await released.Task;
        }, default);
        await rollbackStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.False(attempt.IsCompleted);
        released.SetResult();
        Assert.Equal("VPN_PROBE_FAILED", (await Assert.ThrowsAsync<ClientFault>(() => attempt)).Code);
    }

    [Fact]
    public async Task EstablishedSessionFailureCannotReleaseItsProtection()
    {
        var error = await Assert.ThrowsAsync<ClientFault>(() => InitialConnectionAttempt.RunAsync(false,
            () => throw new ClientFault("CORE_EXITED"), _ => throw new InvalidOperationException("Protection released"), default));
        Assert.Equal("CORE_EXITED", error.Code);
    }

    [Fact]
    public async Task CallerTimeoutPreservesProtectionEvenWhenAProbeFailureArrivesAtTheSameTime()
    {
        using var cancellation = new CancellationTokenSource();
        var error = await Assert.ThrowsAsync<ClientFault>(() => InitialConnectionAttempt.RunAsync(true,
            () => { cancellation.Cancel(); throw new ClientFault("VPN_PROBE_FAILED"); },
            _ => throw new InvalidOperationException("Unknown outcome released protection"), cancellation.Token));
        Assert.Equal("VPN_PROBE_FAILED", error.Code);
    }

    [Fact]
    public async Task FailedRollbackReportsCleanupPendingInsteadOfPretendingNetworkWasRestored()
    {
        var error = await Assert.ThrowsAsync<ClientFault>(() => InitialConnectionAttempt.RunAsync(true,
            () => throw new ClientFault("VPN_DNS_FAILED"), _ => throw new ClientFault("TUN_CLEANUP_PENDING"), default));
        Assert.Equal("TUN_CLEANUP_PENDING", error.Code);
    }
}
