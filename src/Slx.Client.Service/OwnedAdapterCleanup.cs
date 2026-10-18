using Slx.Client.Core;

namespace Slx.Client.Service;

internal static class OwnedAdapterCleanup
{
    public static async Task WaitAsync(string? ownedId, Func<string, bool> exists,
        CancellationToken cancellationToken, TimeSpan? timeout = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (ownedId is null) return;
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(timeout ?? TimeSpan.FromSeconds(5));
        try
        {
            while (exists(ownedId)) await Task.Delay(50, budget.Token);
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new ClientFault("TUN_CLEANUP_PENDING"); }
    }
}
