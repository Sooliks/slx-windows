using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using Slx.Client.Core;
using Slx.Client.Desktop.Presentation;
using Xunit;
namespace Slx.Client.UiTests;
internal static class IconLoadingChecks
{
    public static void Run()
    {
        CheckSharedWorkers();
        CheckCacheAndFailure();
        CheckLazyDispatchAndDisposal();
        CheckPathReplacement();
        CheckAbandonedEntry();
    }
    private static void CheckSharedWorkers()
    {
        using var release = new ManualResetEventSlim();
        using var entered = new CountdownEvent(2);
        using var canceled = new CancellationTokenSource();
        var expected = CreateIcon(); var origin = Environment.CurrentManagedThreadId;
        int calls = 0, active = 0, maximum = 0, onOrigin = 0;
        var cache = new LocalIconCache(LocalIcon.Fallback, _ =>
        {
            if (Environment.CurrentManagedThreadId == origin) Interlocked.Increment(ref onOrigin);
            var running = Interlocked.Increment(ref active);
            InterlockedExtensions.Max(ref maximum, running);
            if (Interlocked.Increment(ref calls) <= 2) entered.Signal();
            try { release.Wait(TimeSpan.FromSeconds(5)); return expected; }
            finally { Interlocked.Decrement(ref active); }
        });
        var first = cache.LoadAsync("first"); var duplicate = cache.LoadAsync("FIRST");
        var second = cache.LoadAsync("second"); var queued = cache.LoadAsync("third");
        var canceledConsumer = cache.LoadAsync("first", canceled.Token);
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(3)));
            Assert.Same(first, duplicate); Assert.False(first.IsCompleted); Assert.False(queued.IsCompleted);
            var read = Task.Run(() => cache.TryCached("uncached", out _));
            Assert.True(read.Wait(TimeSpan.FromSeconds(1))); Assert.False(read.Result);
            Assert.Equal(2, Volatile.Read(ref calls)); Assert.Equal(2, Volatile.Read(ref maximum));
            canceled.Cancel();
            Assert.ThrowsAny<OperationCanceledException>(() => canceledConsumer.GetAwaiter().GetResult());
            Assert.False(first.IsCanceled);
        }
        finally { release.Set(); Task.WhenAll(first, second, queued).GetAwaiter().GetResult(); }
        Assert.Same(expected, first.Result); Assert.Same(expected, duplicate.Result);
        Assert.Equal(3, calls); Assert.Equal(2, maximum); Assert.Equal(0, onOrigin);
    }
    private static void CheckCacheAndFailure()
    {
        var image = CreateIcon(); int extractions = 0;
        var cache = new LocalIconCache(LocalIcon.Fallback, _ => { extractions++; return image; }, 2);
        cache.LoadAsync("first").GetAwaiter().GetResult(); cache.LoadAsync("second").GetAwaiter().GetResult();
        Assert.True(cache.TryCached("FIRST", out var same)); Assert.Same(image, same);
        cache.LoadAsync("third").GetAwaiter().GetResult();
        Assert.True(cache.TryCached("first", out _)); Assert.False(cache.TryCached("second", out _));
        Assert.Same(image, cache.LoadAsync("first").GetAwaiter().GetResult()); Assert.Equal(3, extractions);
        var failures = new LocalIconCache(LocalIcon.Fallback, _ => throw new System.IO.IOException());
        Assert.Same(LocalIcon.Fallback, failures.LoadAsync("failed").GetAwaiter().GetResult());
        Assert.Same(LocalIcon.Fallback, LocalIcon.Load(@"\\untrusted.invalid\share\app.exe"));
        Assert.Same(LocalIcon.Fallback, LocalIcon.Load("relative.exe"));
        Assert.True(LocalIcon.Fallback.IsFrozen);
    }
    private static void CheckLazyDispatchAndDisposal()
    {
        var pending = new TaskCompletionSource<ImageSource>(TaskCreationOptions.RunContinuationsAsynchronously);
        int requests = 0, changes = 0, notifiedThread = 0;
        var rule = new AppRule(Guid.NewGuid(), "Async icon", @"C:\Synthetic Library\Async.exe");
        using var entry = new LibraryEntry(rule.Name, rule, null)
        { IconLoader = (_, token) => { requests++; return pending.Task.WaitAsync(token); } };
        entry.PropertyChanged += (_, args) =>
        { if (args.PropertyName == nameof(LibraryEntry.Icon)) { changes++; notifiedThread = Environment.CurrentManagedThreadId; } };
        Assert.Equal(0, requests); Assert.Same(LocalIcon.Fallback, entry.Icon); Assert.Same(entry.Icon, entry.Icon);
        Assert.Equal(1, requests); Assert.Equal(0, changes);
        var image = CreateIcon(); pending.SetResult(image);
        PumpUntil(() => changes == 1);
        Assert.Same(image, entry.Icon); Assert.Equal(Environment.CurrentManagedThreadId, notifiedThread);
        var later = new TaskCompletionSource<ImageSource>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken canceled = default; int lateChanges = 0;
        var disposed = new LibraryEntry(rule.Name, rule, null)
        { IconLoader = (_, token) => { canceled = token; return later.Task.WaitAsync(token); } };
        disposed.PropertyChanged += (_, _) => lateChanges++;
        var initial = disposed.Icon; disposed.Dispose(); disposed.Dispose();
        Assert.True(canceled.IsCancellationRequested); later.SetResult(image); Sta.Pump();
        Assert.Same(initial, disposed.Icon); Assert.Equal(0, lateChanges);
    }
    private static void CheckAbandonedEntry()
    {
        var pending = new TaskCompletionSource<ImageSource>(TaskCreationOptions.RunContinuationsAsynchronously);
        var weak = CreateAbandoned(pending);
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        Assert.False(weak.TryGetTarget(out _));
        pending.SetResult(CreateIcon()); Sta.Pump();
    }
    private static void CheckPathReplacement()
    {
        var old = new AppRule(Guid.NewGuid(), "Changing", @"C:\Synthetic Library\Changing-old.exe");
        var current = old with { ExecutablePath = @"C:\Synthetic Library\Changing-current.exe" };
        var first = new TaskCompletionSource<ImageSource>(TaskCreationOptions.RunContinuationsAsynchronously);
        var second = new TaskCompletionSource<ImageSource>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken canceled = default;
        using var entry = new LibraryEntry(old.Name, old, null, [old])
        {
            IconLoader = (path, token) =>
            {
                if (path == old.ExecutablePath) { canceled = token; return first.Task.WaitAsync(token); }
                return second.Task.WaitAsync(token);
            }
        };
        _ = entry.Icon; entry.UpdatePaths(current, [current]);
        Assert.True(canceled.IsCancellationRequested); Assert.Equal(current, entry.Rule);
        Assert.Equal(current.ExecutablePath, entry.Detail); _ = entry.Icon;
        var image = CreateIcon(); first.SetResult(CreateIcon()); second.SetResult(image);
        PumpUntil(() => ReferenceEquals(image, entry.Icon));
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<LibraryEntry> CreateAbandoned(TaskCompletionSource<ImageSource> pending)
    {
        var rule = new AppRule(Guid.NewGuid(), "Abandoned", @"C:\Synthetic Library\Abandoned.exe");
        var entry = new LibraryEntry(rule.Name, rule, null) { IconLoader = (_, token) => pending.Task.WaitAsync(token) };
        _ = entry.Icon; return new WeakReference<LibraryEntry>(entry);
    }
    private static ImageSource CreateIcon()
    {
        var image = new DrawingImage(new GeometryDrawing(Brushes.Lime, null, new RectangleGeometry(new Rect(0, 0, 16, 16))));
        image.Freeze(); return image;
    }
    private static void PumpUntil(Func<bool> condition)
    {
        var elapsed = Stopwatch.StartNew();
        while (!condition() && elapsed.Elapsed < TimeSpan.FromSeconds(3)) { Sta.Pump(); Thread.Sleep(1); }
        Assert.True(condition());
    }
    private static class InterlockedExtensions
    {
        public static void Max(ref int target, int value)
        {
            int current;
            do { current = Volatile.Read(ref target); if (current >= value) return; }
            while (Interlocked.CompareExchange(ref target, value, current) != current);
        }
    }
}
