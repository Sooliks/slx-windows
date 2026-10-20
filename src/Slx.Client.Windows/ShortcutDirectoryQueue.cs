namespace Slx.Client.Windows;

/// <summary>Bounds the whole directory walk, including trees that contain no shortcuts.</summary>
internal sealed class ShortcutDirectoryQueue
{
    private const int DirectoryLimit = 2048;
    private readonly Queue<(string Path, int Depth)> pending = new();
    private int scheduled;

    public ShortcutDirectoryQueue(IEnumerable<string> roots)
    {
        foreach (var root in roots.Take(DirectoryLimit))
        {
            pending.Enqueue((root, 0));
            scheduled++;
        }
    }

    public bool TryDequeue(out (string Path, int Depth) item) => pending.TryDequeue(out item);

    public void AddChildren(IEnumerable<string> paths, int parentDepth, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (parentDepth >= 5 || scheduled >= DirectoryLimit) return;
        foreach (var path in paths.Take(200))
        {
            cancellationToken.ThrowIfCancellationRequested();
            pending.Enqueue((path, parentDepth + 1));
            if (++scheduled == DirectoryLimit) break;
        }
    }
}
