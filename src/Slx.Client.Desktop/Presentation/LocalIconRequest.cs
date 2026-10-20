using System.Windows.Media;
using System.Windows.Threading;
namespace Slx.Client.Desktop.Presentation;
internal sealed class LocalIconRequest : IDisposable
{
    private readonly CancellationTokenSource cancellation = new();
    private bool disposed;
    public static LocalIconRequest Start<T>(T target, string path, Action<T, ImageSource> apply,
        Func<string, CancellationToken, Task<ImageSource>>? load = null) where T : class
    {
        var request = new LocalIconRequest();
        var dispatcher = Dispatcher.FromThread(Thread.CurrentThread);
        if (dispatcher is not null)
            _ = PopulateAsync(new WeakReference<T>(target), new WeakReference<Dispatcher>(dispatcher), path,
                apply, load ?? LocalIcon.LoadAsync, request.cancellation.Token);
        return request;
    }
    private static async Task PopulateAsync<T>(WeakReference<T> target, WeakReference<Dispatcher> origin, string path,
        Action<T, ImageSource> apply, Func<string, CancellationToken, Task<ImageSource>> load, CancellationToken token) where T : class
    {
        try
        {
            var image = await load(path, token).ConfigureAwait(false);
            if (token.IsCancellationRequested || !origin.TryGetTarget(out var dispatcher) || dispatcher.HasShutdownStarted) return;
            await dispatcher.InvokeAsync(() =>
            {
                if (!token.IsCancellationRequested && target.TryGetTarget(out var current)) apply(current, image);
            }, DispatcherPriority.Background, token).Task.ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }
        catch (InvalidOperationException) when (!origin.TryGetTarget(out var dispatcher) || dispatcher.HasShutdownStarted) { }
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; cancellation.Cancel(); cancellation.Dispose();
    }
}
