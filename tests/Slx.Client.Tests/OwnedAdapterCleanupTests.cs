using Slx.Client.Core;
using Slx.Client.Service;
using Xunit;

namespace Slx.Client.Tests;

public sealed class OwnedAdapterCleanupTests
{
    [Fact]
    public async Task ReconnectWaitsUntilTheStoppedChildsAdapterDisappears()
    {
        int observations = 0;
        await OwnedAdapterCleanup.WaitAsync("owned-guid", id =>
        {
            Assert.Equal("owned-guid", id);
            return ++observations < 4;
        }, default);
        Assert.Equal(4, observations);
    }

    [Fact]
    public async Task PersistentAdapterFailsClosedAfterBoundedDeadline()
    {
        var error = await Assert.ThrowsAsync<ClientFault>(() => OwnedAdapterCleanup.WaitAsync(
            "owned-guid", _ => true, default, TimeSpan.FromMilliseconds(70)));
        Assert.Equal("TUN_CLEANUP_PENDING", error.Code);
    }

    [Fact]
    public async Task CancellationIsHonoredWithoutChangingAnyAdapter()
    {
        using var canceled = new CancellationTokenSource(TimeSpan.FromMilliseconds(20));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => OwnedAdapterCleanup.WaitAsync(
            "owned-guid", _ => true, canceled.Token));
    }

    [Fact]
    public async Task AnUnrelatedAdapterIsNeverQueriedOrRemoved()
    {
        var interfaces = new Dictionary<string, bool> { ["owned-guid"] = false, ["foreign-guid"] = true };
        await OwnedAdapterCleanup.WaitAsync("owned-guid", id =>
        {
            Assert.Equal("owned-guid", id);
            return interfaces[id];
        }, default);
        Assert.True(interfaces["foreign-guid"]);
    }

    [Fact]
    public async Task NoOwnedLaunchDoesNotWaitForAnExistingForeignAdapter()
    {
        await OwnedAdapterCleanup.WaitAsync(null, _ => throw new InvalidOperationException("Must not inspect foreign adapters"), default);
    }
}
