using Slx.Client.Core;

namespace Slx.Client.Service;

internal static class StartupReadiness
{
    internal static async Task<bool> CheckAsync(
        Func<ProbeTarget, CancellationToken, Task<ProbeObservation>> check, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Both observations use the authenticated internal proxy and the existing strict HTTPS
        // origin/status checks. One remote health origin being unavailable must not reject a working VPN.
        using var pending = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var primary = check(ProbeTarget.Gstatic, pending.Token);
        var alternate = check(ProbeTarget.Slx, pending.Token);
        var first = await Task.WhenAny(primary, alternate);
        var second = ReferenceEquals(first, primary) ? alternate : primary;
        try
        {
            bool passed = (await first).Passed;
            if (passed) pending.Cancel();
            else passed = (await second).Passed;
            cancellationToken.ThrowIfCancellationRequested();
            return passed;
        }
        finally
        {
            pending.Cancel();
            // Observe both tasks. Internal cancellation of an unneeded peer is not a failed VPN probe.
            try { await Task.WhenAll(primary, alternate); }
            catch (OperationCanceledException) when (pending.IsCancellationRequested && !cancellationToken.IsCancellationRequested) { }
            cancellationToken.ThrowIfCancellationRequested();
        }
    }
}
