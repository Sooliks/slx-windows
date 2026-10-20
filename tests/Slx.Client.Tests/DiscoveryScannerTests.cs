using System.Collections.Concurrent;
using Slx.Client.Core;
using Slx.Client.Windows;
using Xunit;

namespace Slx.Client.Tests;

public sealed class DiscoveryScannerTests
{
    [Fact]
    public async Task FastSourcesPublishWhileAnotherNativeSourceIsStillBlocked()
    {
        using var blocked = new ManualResetEventSlim();
        var fast = Signal();
        var batches = new ConcurrentQueue<DiscoveryBatch>();
        var scans = Sources(source => source == DiscoverySource.Installed
            ? _ => { blocked.Wait(); return []; }
            : _ => [App(source)]);
        var operation = DiscoveryScanner.RunAsync(scans, batch =>
        {
            batches.Enqueue(batch);
            if (batch.Source == DiscoverySource.Running) fast.TrySetResult();
        }, CancellationToken.None);
        try
        {
            await fast.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(operation.IsCompleted);
            Assert.Contains(batches, x => x.Source == DiscoverySource.Running && x.Success);
            Assert.DoesNotContain(batches, x => x.Source == DiscoverySource.Installed);
        }
        finally { blocked.Set(); await operation; }
        Assert.Equal(6, batches.Count);
        Assert.All(batches, x => Assert.True(x.Success));
    }

    [Theory]
    [InlineData("denied")]
    [InlineData("io")]
    [InlineData("native")]
    public async Task ExpectedSourceFailureDoesNotDiscardOtherSources(string kind)
    {
        Exception failure = kind switch
        {
            "denied" => new UnauthorizedAccessException(),
            "io" => new IOException(),
            _ => new System.ComponentModel.Win32Exception(5)
        };
        var scans = Sources(source => source == DiscoverySource.Shortcuts ? _ => throw failure : _ => [App(source)]);
        var batches = new ConcurrentQueue<DiscoveryBatch>();
        await DiscoveryScanner.RunAsync(scans, batches.Enqueue, CancellationToken.None);
        var failed = Assert.Single(batches, x => !x.Success);
        Assert.Equal(DiscoverySource.Shortcuts, failed.Source);
        Assert.Empty(failed.Apps);
        Assert.Equal(5, batches.Count(x => x.Success));
    }

    [Fact]
    public async Task NativeScansHaveExactlyThreeConcurrentWorkers()
    {
        using var release = new ManualResetEventSlim();
        var entered = Signal();
        int active = 0, started = 0, peak = 0;
        var scans = Sources(_ => token =>
        {
            int count = Interlocked.Increment(ref active);
            int observed;
            do { observed = Volatile.Read(ref peak); }
            while (count > observed && Interlocked.CompareExchange(ref peak, count, observed) != observed);
            if (Interlocked.Increment(ref started) == 3) entered.TrySetResult();
            try { release.Wait(token); return []; }
            finally { Interlocked.Decrement(ref active); }
        });
        var operation = DiscoveryScanner.RunAsync(scans, _ => { }, CancellationToken.None);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(3, Volatile.Read(ref started));
            Assert.Equal(3, Volatile.Read(ref active));
        }
        finally { release.Set(); await operation; }
        Assert.Equal(6, started);
        Assert.Equal(3, peak);
    }

    [Fact]
    public async Task CancellationWaitsForNativeWorkAndNeverPublishesItsLateResult()
    {
        using var release = new ManualResetEventSlim();
        using var cancellation = new CancellationTokenSource();
        var entered = Signal();
        int started = 0, published = 0;
        var scans = Sources(_ => _ =>
        {
            if (Interlocked.Increment(ref started) == 3) entered.TrySetResult();
            release.Wait(); // Models a bounded native call that cannot observe cancellation itself.
            return [App(DiscoverySource.Running)];
        });
        var operation = DiscoveryScanner.RunAsync(scans, _ => Interlocked.Increment(ref published), cancellation.Token);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            cancellation.Cancel();
            Assert.False(operation.IsCompleted);
        }
        finally { cancellation.Cancel(); release.Set(); }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);
        Assert.Equal(3, started);
        Assert.Equal(0, published);
    }

    [Fact]
    public async Task UnexpectedFaultIsNotHiddenAndStillWaitsForStartedNativeWork()
    {
        using var release = new ManualResetEventSlim();
        var entered = Signal();
        var scans = new DiscoverySourceScan[]
        {
            new(DiscoverySource.Running, _ => throw new InvalidOperationException()),
            new(DiscoverySource.Installed, _ => { entered.TrySetResult(); release.Wait(); return []; })
        };
        var operation = DiscoveryScanner.RunAsync(scans, _ => { }, CancellationToken.None);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(operation.IsCompleted);
        }
        finally { release.Set(); }
        await Assert.ThrowsAsync<InvalidOperationException>(() => operation);
    }

    [Fact]
    public async Task AlreadyCancelledOperationNeverStartsNativeWork()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        int started = 0;
        var scans = Sources(_ => _ => { Interlocked.Increment(ref started); return []; });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            DiscoveryScanner.RunAsync(scans, _ => { }, cancellation.Token));
        Assert.Equal(0, started);
    }

    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static AppRule App(DiscoverySource source) => CoreFixtures.App() with { Name = source.ToString() };
    private static DiscoverySourceScan[] Sources(Func<DiscoverySource, Func<CancellationToken, IReadOnlyList<AppRule>>> find) =>
        Enum.GetValues<DiscoverySource>().Select(source => new DiscoverySourceScan(source, find(source))).ToArray();
}
