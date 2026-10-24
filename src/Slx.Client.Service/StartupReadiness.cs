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
        var primary = check(ProbeTarget.Gstatic, cancellationToken);
        var alternate = check(ProbeTarget.Slx, cancellationToken);
        await Task.WhenAll(primary, alternate);
        cancellationToken.ThrowIfCancellationRequested();
        return (await primary).Passed || (await alternate).Passed;
    }
}
