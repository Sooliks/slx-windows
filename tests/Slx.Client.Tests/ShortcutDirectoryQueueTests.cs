using Slx.Client.Windows;
using Xunit;

namespace Slx.Client.Tests;

public sealed class ShortcutDirectoryQueueTests
{
    [Fact]
    public void TreeWithoutAnyShortcutsStillHasAFiniteGlobalDirectoryBudget()
    {
        var queue = new ShortcutDirectoryQueue(["root"]);
        int visited = 0, enumerated = 0;
        while (queue.TryDequeue(out var item))
        {
            visited++;
            queue.AddChildren(Children(), item.Depth, CancellationToken.None);
        }
        Assert.Equal(2048, visited);
        Assert.Equal(2047, enumerated);
        IEnumerable<string> Children()
        {
            for (int i = 0; i < 1000; i++) { enumerated++; yield return "child"; }
        }
    }

    [Fact]
    public void ADeepSingleBranchStopsAtTheExistingDepthBoundary()
    {
        var queue = new ShortcutDirectoryQueue(["root"]);
        var depths = new List<int>();
        while (queue.TryDequeue(out var item))
        {
            depths.Add(item.Depth);
            queue.AddChildren(["child"], item.Depth, CancellationToken.None);
        }
        Assert.Equal(new[] { 0, 1, 2, 3, 4, 5 }, depths);
    }

    [Fact]
    public void CancellationStopsDirectoryEnumerationBeforeItCanGrowTheQueue()
    {
        var queue = new ShortcutDirectoryQueue(["root"]);
        Assert.True(queue.TryDequeue(out var item));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => queue.AddChildren(["child"], item.Depth, cancellation.Token));
        Assert.False(queue.TryDequeue(out _));
    }
}
