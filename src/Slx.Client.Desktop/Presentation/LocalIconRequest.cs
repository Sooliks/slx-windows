using System.Windows.Media;
using System.Windows.Threading;
namespace Slx.Client.Desktop.Presentation;
internal sealed class LocalIconRequest : IDisposable
{
    private readonly CancellationTokenSource cancellation = new();
    private bool disposed;
    public static LocalIconRequest Start<T>(T target, string path, Action<T, ImageSource> apply,
        Func<string, CancellationToken, Task<ImageSource>>? load = null) where T : class
        => Start(target, [path], apply, load);
    public static LocalIconRequest Start<T>(T target, IEnumerable<string> paths, Action<T, ImageSource> apply,
        Func<string, CancellationToken, Task<ImageSource>>? load = null,
        Func<CancellationToken, Task>? retryDelay = null) where T : class
    {
        var request = new LocalIconRequest();
        var dispatcher = Dispatcher.FromThread(Thread.CurrentThread);
        if (dispatcher is not null)
            _ = PopulateAsync(new WeakReference<T>(target), new WeakReference<Dispatcher>(dispatcher),
                paths.Take(64).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(), apply, load ?? LocalIcon.LoadAsync,
                retryDelay ?? (token => Task.Delay(LocalIconCache.RetryDelay + TimeSpan.FromMilliseconds(50), token)), request.cancellation.Token);
        return request;
    }
    private static async Task PopulateAsync<T>(WeakReference<T> target, WeakReference<Dispatcher> origin, IReadOnlyList<string> paths,
        Action<T, ImageSource> apply, Func<string, CancellationToken, Task<ImageSource>> load,
        Func<CancellationToken, Task> retryDelay, CancellationToken token) where T : class
    {
        try
        {
            var image = await LoadCandidatesAsync(paths, load, token).ConfigureAwait(false);
            if (ReferenceEquals(image, LocalIcon.Fallback) && CanContinue(target, origin, token))
            {
                await retryDelay(token).ConfigureAwait(false);
                if (!CanContinue(target, origin, token)) return;
                image = await LoadCandidatesAsync(paths, load, token).ConfigureAwait(false);
            }
            if (token.IsCancellationRequested || !origin.TryGetTarget(out var dispatcher) || dispatcher.HasShutdownStarted) return;
            await dispatcher.InvokeAsync(() =>
            {
                if (!token.IsCancellationRequested && target.TryGetTarget(out var current)) apply(current, image);
            }, DispatcherPriority.Background, token).Task.ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }
        catch (InvalidOperationException) when (!origin.TryGetTarget(out var dispatcher) || dispatcher.HasShutdownStarted) { }
    }
    private static bool CanContinue<T>(WeakReference<T> target, WeakReference<Dispatcher> origin, CancellationToken token) where T : class
        => !token.IsCancellationRequested && target.TryGetTarget(out _) && origin.TryGetTarget(out var dispatcher)
            && !dispatcher.HasShutdownStarted;
    private static async Task<ImageSource> LoadCandidatesAsync(IReadOnlyList<string> paths,
        Func<string, CancellationToken, Task<ImageSource>> load, CancellationToken token)
    {
        foreach (var path in paths)
        {
            token.ThrowIfCancellationRequested();
            var image = await load(path, token).ConfigureAwait(false);
            if (!ReferenceEquals(image, LocalIcon.Fallback)) return image;
        }
        return LocalIcon.Fallback;
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; cancellation.Cancel(); cancellation.Dispose();
    }
}
