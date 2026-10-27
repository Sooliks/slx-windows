using Slx.Client.Core;
namespace Slx.Client.Service;

internal sealed record ConnectionHealthResult(HealthConfirmationResult Confirmation, bool Retained, bool Deferred = false);

internal static class ConnectionHealthCheck
{
    internal static async Task<ConnectionHealthResult> CheckAsync(TunnelStatus connected,
        Func<ProbeTarget, CancellationToken, Task<ProbeObservation>> check,
        Func<bool> resourcesReady, Action<TunnelStatus> publish, CancellationToken cancellationToken,
        Func<CancellationToken, Task>? pause = null, Func<bool>? deferFailure = null)
    {
        var observed = connected;
        void Publish(TunnelPhase phase, bool passed)
        {
            observed = observed with { Phase = phase, ObservedAt = DateTimeOffset.UtcNow, ProbePassed = passed };
            publish(observed);
        }
        // Confirmation invalidates readiness while the established core and protection remain owned.
        // A failed confirmation or lost owned resources authorizes the existing recovery path.
        var result = await HealthConfirmation.CheckAsync(check,
            () => Publish(TunnelPhase.Checking, false), cancellationToken, pause);
        bool retained = result.Passed && resourcesReady();
        bool deferred = false;
        if (!result.Passed && deferFailure is not null && resourcesReady())
        { cancellationToken.ThrowIfCancellationRequested(); deferred = deferFailure(); }
        cancellationToken.ThrowIfCancellationRequested();
        Publish(retained ? TunnelPhase.Connected : deferred ? TunnelPhase.Checking : TunnelPhase.Reconnecting, retained);
        return new(result, retained, deferred);
    }
}
