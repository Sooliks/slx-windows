using System.Windows.Media;
namespace Slx.Client.Desktop.Presentation;
internal sealed class LocalIconCache(ImageSource fallback, Func<string, ImageSource?> extract, int capacity = 512,
    Func<long>? milliseconds = null)
{
    internal static TimeSpan RetryDelay { get; } = TimeSpan.FromSeconds(2);
    private readonly Func<long> clock = milliseconds ?? (() => Environment.TickCount64);
    private readonly object sync = new();
    private readonly SemaphoreSlim workers = new(2, 2);
    private readonly Dictionary<string, (ImageSource Image, LinkedListNode<string> Node, long RetryAt)> images = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Task<ImageSource>> pending = new(StringComparer.OrdinalIgnoreCase);
    private readonly LinkedList<string> recent = new();
    public bool TryCached(string path, out ImageSource image)
    {
        lock (sync)
        {
            if (images.TryGetValue(path, out var entry))
            {
                if (ReferenceEquals(entry.Image, fallback) && clock() >= entry.RetryAt)
                { images.Remove(path); recent.Remove(entry.Node); image = fallback; return false; }
                recent.Remove(entry.Node); recent.AddLast(entry.Node); image = entry.Image; return true;
            }
        }
        image = fallback; return false;
    }
    public Task<ImageSource> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Task<ImageSource> task;
        lock (sync)
        {
            if (TryCached(path, out var image)) return Task.FromResult(image);
            if (!pending.TryGetValue(path, out task!)) pending[path] = task = Task.Run(() => ExtractAsync(path));
        }
        return cancellationToken.CanBeCanceled ? task.WaitAsync(cancellationToken) : task;
    }
    private async Task<ImageSource> ExtractAsync(string path)
    {
        await workers.WaitAsync().ConfigureAwait(false);
        ImageSource image;
        try
        {
            image = extract(path) ?? fallback;
            if (!image.IsFrozen)
            {
                if (image.CanFreeze) image.Freeze();
                else image = fallback;
            }
        }
        // Shell icon providers are decorative and may fail independently of application discovery.
        catch (Exception) { image = fallback; }
        finally { workers.Release(); }
        lock (sync)
        {
            if (images.Count >= capacity && recent.First is { } oldest)
            { images.Remove(oldest.Value); recent.RemoveFirst(); }
            images[path] = (image, recent.AddLast(path), clock() + (long)RetryDelay.TotalMilliseconds); pending.Remove(path);
        }
        return image;
    }
}
