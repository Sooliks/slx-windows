using Slx.Client.Core;
using Slx.Client.Service;
using Xunit;

namespace Slx.Client.Tests;

public sealed class BackgroundCleanupTests
{
    [Theory]
    [InlineData("TUN_CLEANUP_PENDING")]
    [InlineData("CORE_STOP_FAILED")]
    public async Task KnownCleanupFailureKeepsFaultStateAndDoesNotEscapeControlLoop(string code)
    {
        var faulted = new TunnelStatus(TunnelPhase.Faulted, DateTimeOffset.UtcNow, Code: code);
        TunnelStatus state = new(TunnelPhase.Connected, DateTimeOffset.UtcNow, ProbePassed: true);
        int attempts = 0;
        bool completed = await BackgroundCleanup.TryAsync(() =>
        {
            attempts++;
            state = faulted;
            throw new ClientFault(code);
        });
        Assert.False(completed);
        Assert.Same(faulted, state);
        Assert.Equal(1, attempts);
        Assert.False(state.ProbePassed);
    }

    [Fact]
    public async Task UnexpectedErrorsAreNotSilentlyHidden()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => BackgroundCleanup.TryAsync(
            () => throw new InvalidOperationException("Synthetic unknown cleanup failure")));
        await Assert.ThrowsAsync<ClientFault>(() => BackgroundCleanup.TryAsync(
            () => throw new ClientFault("UNEXPECTED_FAULT")));
    }

    [Fact]
    public async Task CompletedCleanupReturnsNormally()
    {
        Assert.True(await BackgroundCleanup.TryAsync(() => Task.CompletedTask));
    }
}
