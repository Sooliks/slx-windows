using System.Diagnostics;
using Slx.Client.Core;

namespace Slx.Client.Service;

internal sealed partial class TunnelEngine
{
    private readonly UpstreamStallHold upstreamHold = new();
    private EstablishedProbe? establishedProbe;

    private async Task<bool> CheckOwnedHealthAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        healthAt = Stopwatch.GetTimestamp() + 15 * Stopwatch.Frequency;
        using var deadline = upstreamHold.Pending
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken) : null;
        if (deadline is not null)
        {
            var remaining = upstreamHold.Remaining;
            if (remaining == TimeSpan.Zero) deadline.Cancel();
            else deadline.CancelAfter(remaining);
        }
        var token = deadline?.Token ?? cancellationToken;
        bool failedProbes;
        try
        {
            token.ThrowIfCancellationRequested();
            var kept = establishedProbe ??= new EstablishedProbe(probe.Port, probe.User, probe.Password);
            var result = await ConnectionHealthCheck.CheckAsync(state,
                (target, stop) => LocalProbe.ObserveAsync(probe.Port, probe.User, probe.Password, target, stop),
                () => HealthResourcesReady(token), observed => state = observed, token,
                deferFailure: upstreamHold.Hold, established: kept.ObserveAsync);
            RecordHealthIncident(result.Confirmation, result.Retained);
            if (result.Retained) { upstreamHold.Reset(); return true; }
            if (result.Deferred)
            {
                var remaining = upstreamHold.Remaining;
                var delay = remaining < UpstreamStallHold.RecheckInterval ? remaining : UpstreamStallHold.RecheckInterval;
                healthAt = Stopwatch.GetTimestamp() + (long)(delay.TotalSeconds * Stopwatch.Frequency);
                return true;
            }
            failedProbes = !result.Confirmation.Passed;
        }
        catch (OperationCanceledException) when (deadline?.IsCancellationRequested == true && !cancellationToken.IsCancellationRequested)
        {
            state = state with { Phase = TunnelPhase.Reconnecting, ProbePassed = false, ObservedAt = DateTimeOffset.UtcNow };
            failedProbes = true;
        }
        upstreamHold.Reset();
        await CheckFrontControlAsync(cancellationToken);
        if (failedProbes && FrontAlive && CoreAlive)
        {
            transportExecution.Failed();
            rotation.Failed("HEALTH_PROBE_FAILED");
            PublishAttempt();
        }
        return false;
    }

    private void ResetEstablishedHealth()
    {
        upstreamHold.Reset();
        establishedProbe?.Dispose();
        establishedProbe = null;
    }
}
