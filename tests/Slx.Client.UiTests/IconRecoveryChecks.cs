using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using Slx.Client.Core;
using Slx.Client.Desktop.Presentation;
using Slx.Client.Desktop.ViewModels;
using Xunit;
namespace Slx.Client.UiTests;
internal static class IconRecoveryChecks
{
    public static void Run()
    {
        CheckNegativeExpiry();
        CheckBoundedRetry();
        CheckCanceledRetry();
        CheckAbandonedRetry();
        CheckMemberChanges();
        CheckExplicitRefresh();
        CheckNativeExecutableAndSelectedGroup();
        CheckNativeFileAppears();
    }
    private static void CheckNegativeExpiry()
    {
        long now = 0; int calls = 0; var expected = Icon();
        var cache = new LocalIconCache(LocalIcon.Fallback, _ => ++calls == 1 ? null : expected,
            milliseconds: () => now);
        Assert.Same(LocalIcon.Fallback, cache.LoadAsync("retry").GetAwaiter().GetResult());
        now = 1999;
        Assert.Same(LocalIcon.Fallback, cache.LoadAsync("RETRY").GetAwaiter().GetResult()); Assert.Equal(1, calls);
        now = 2000; Assert.False(cache.TryCached("retry", out _));
        Assert.Same(expected, cache.LoadAsync("retry").GetAwaiter().GetResult()); Assert.Equal(2, calls);
        now = 100000; Assert.Same(expected, cache.LoadAsync("retry").GetAwaiter().GetResult()); Assert.Equal(2, calls);
    }
    private static void CheckBoundedRetry()
    {
        var delay = Completion(); var target = new Target(); var expected = Icon(); int calls = 0, delays = 0;
        using var request = LocalIconRequest.Start(target, new[] { "missing", "MISSING" }, Apply,
            (_, _) => Task.FromResult(++calls == 1 ? LocalIcon.Fallback : expected),
            token => { delays++; return delay.Task.WaitAsync(token); });
        Assert.Equal(1, calls); Assert.Equal(1, delays); Assert.Equal(0, target.Applied);
        delay.SetResult(); PumpUntil(() => target.Applied == 1);
        Assert.Equal(2, calls); Assert.Equal(1, delays); Assert.Same(expected, target.Image);
        var failed = new Target(); var later = Completion(); int failedCalls = 0;
        using var missing = LocalIconRequest.Start(failed, new[] { "absent" }, Apply,
            (_, _) => { failedCalls++; return Task.FromResult(LocalIcon.Fallback); }, token => later.Task.WaitAsync(token));
        later.SetResult(); PumpUntil(() => failed.Applied == 1); Sta.Pump();
        Assert.Equal(2, failedCalls); Assert.Same(LocalIcon.Fallback, failed.Image);
    }
    private static void CheckCanceledRetry()
    {
        var delay = Completion(); var target = new Target(); int calls = 0; CancellationToken waitToken = default;
        var request = LocalIconRequest.Start(target, new[] { "missing" }, Apply,
            (_, _) => { calls++; return Task.FromResult(LocalIcon.Fallback); },
            token => { waitToken = token; return delay.Task.WaitAsync(token); });
        request.Dispose(); Assert.True(waitToken.IsCancellationRequested);
        delay.SetResult(); Sta.Pump(); Assert.Equal(1, calls); Assert.Equal(0, target.Applied);
    }
    private static void CheckAbandonedRetry()
    {
        var delay = Completion(); var weak = AbandonedRetry(delay);
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        Assert.False(weak.TryGetTarget(out _)); delay.SetResult(); Sta.Pump();
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<Target> AbandonedRetry(TaskCompletionSource delay)
    {
        var target = new Target();
        _ = LocalIconRequest.Start(target, new[] { "missing" }, Apply,
            (_, _) => Task.FromResult(LocalIcon.Fallback), token => delay.Task.WaitAsync(token));
        return new(target);
    }
    private static void CheckMemberChanges()
    {
        var first = new AppRule(Guid.NewGuid(), "Group", @"C:\Synthetic Icons\Missing.exe");
        var second = first with { Id = Guid.NewGuid(), ExecutablePath = @"C:\Synthetic Icons\Current.exe" };
        var expected = Icon(); var requests = new List<string>();
        using var entry = new LibraryEntry(first.Name, first, null, [first])
        {
            IconLoader = (path, _) =>
            {
                requests.Add(path);
                return Task.FromResult(path == second.ExecutablePath ? expected : LocalIcon.Fallback);
            }
        };
        Assert.Same(LocalIcon.Fallback, entry.Icon);
        entry.UpdatePaths(first, [first, second]); Assert.Equal(first, entry.Rule);
        _ = entry.Icon; PumpUntil(() => ReferenceEquals(entry.Icon, expected));
        Assert.Contains(second.ExecutablePath, requests); Assert.Equal(2, entry.Paths?.Count);
        Assert.Equal(first.ExecutablePath, Assert.IsType<AppRule>(entry.Rule).ExecutablePath);
    }
    private static void CheckNativeExecutableAndSelectedGroup()
    {
        var executable = Path.Combine(AppContext.BaseDirectory, "SLX VPN.exe");
        Assert.True(File.Exists(executable));
        var native = LocalIcon.LoadAsync(executable).GetAwaiter().GetResult();
        Assert.NotSame(LocalIcon.Fallback, native); Assert.True(native.IsFrozen);
        var missing = new AppRule(Guid.NewGuid(), "Native icon group",
            Path.Combine(Path.GetTempPath(), "slx-missing-icon-" + Guid.NewGuid().ToString("N"), "Missing.exe"));
        var existing = missing with { Id = Guid.NewGuid(), ExecutablePath = executable };
        var selected = new AppItemViewModel(new[] { missing, existing });
        PumpUntil(() => ReferenceEquals(native, selected.Icon));
        Assert.Equal(missing.ExecutablePath, selected.ExecutablePath);
        Assert.Equal(new[] { missing, existing }, selected.ToRules());
        var growing = new AppItemViewModel(missing);
        Assert.True(growing.AddPath(existing)); PumpUntil(() => ReferenceEquals(native, growing.Icon));
        Assert.Equal(missing.ExecutablePath, growing.ExecutablePath);
        Assert.Equal(new[] { missing, existing }, growing.ToRules());
    }
    private static void CheckExplicitRefresh()
    {
        var rule = new AppRule(Guid.NewGuid(), "Retry same path", @"C:\Synthetic Icons\Retry.exe");
        var expected = Icon(); bool available = false; int requests = 0, notices = 0; CancellationToken previous = default;
        using var entry = new LibraryEntry(rule.Name, rule, null, [rule])
        {
            IconLoader = (path, token) =>
            {
                Assert.Equal(rule.ExecutablePath, path); requests++;
                if (!available) previous = token;
                return Task.FromResult(available ? expected : LocalIcon.Fallback);
            }
        };
        entry.PropertyChanged += (_, args) => { if (args.PropertyName == nameof(LibraryEntry.Icon)) notices++; };
        entry.RefreshIconIfMissing(); Assert.Equal(0, requests); Assert.Equal(0, notices);
        Assert.Same(LocalIcon.Fallback, entry.Icon); Assert.Equal(1, requests);
        available = true; entry.RefreshIconIfMissing(); Assert.True(previous.IsCancellationRequested);
        _ = entry.Icon; PumpUntil(() => ReferenceEquals(expected, entry.Icon));
        Assert.Equal(2, requests); Assert.Equal(rule, entry.Rule); Assert.Equal(new[] { rule }, entry.Paths);
        var delivered = notices; entry.RefreshIconIfMissing(); Sta.Pump();
        Assert.Equal(2, requests); Assert.Equal(delivered, notices);
    }
    private static void CheckNativeFileAppears()
    {
        var directory = Path.Combine(Path.GetTempPath(), "slx-icon-recovery-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "Восстановленное приложение.exe");
        Assert.Same(LocalIcon.Fallback, LocalIcon.LoadAsync(path).GetAwaiter().GetResult());
        Directory.CreateDirectory(directory);
        try
        {
            var target = new Target();
            using var request = LocalIconRequest.Start(target, new[] { path }, Apply);
            File.Copy(Path.Combine(AppContext.BaseDirectory, "SLX VPN.exe"), path);
            PumpUntil(() => target.Applied == 1);
            Assert.NotSame(LocalIcon.Fallback, target.Image); Assert.True(target.Image?.IsFrozen);
        }
        finally { File.Delete(path); Directory.Delete(directory); }
    }
    private static ImageSource Icon() { var image = new DrawingImage(); image.Freeze(); return image; }
    private static TaskCompletionSource Completion() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static void Apply(Target target, ImageSource image) { target.Image = image; target.Applied++; }
    private sealed class Target { public ImageSource? Image; public int Applied; }
    private static void PumpUntil(Func<bool> condition)
    {
        var elapsed = Stopwatch.StartNew();
        while (!condition() && elapsed.Elapsed < TimeSpan.FromSeconds(3)) { Sta.Pump(); Thread.Sleep(1); }
        Assert.True(condition());
    }
}
