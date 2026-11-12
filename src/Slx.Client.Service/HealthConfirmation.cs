using Slx.Client.Core;
namespace Slx.Client.Service;
internal sealed record HealthConfirmationResult(ProbeObservation Primary, ProbeObservation? Repeat = null,
    ProbeObservation? Alternate = null, EstablishedObservation? Established = null)
{
    public bool Passed => Primary.Passed || Repeat?.Passed == true || Alternate?.Passed == true;
    /// <summary>Every new connection failed while an already-open stream through the same core was answered.</summary>
    public bool OnlyEstablishedStreamPassed => !Passed && Established is { Reused: true, Observation.Passed: true };
}
internal static class HealthConfirmation
{
    internal static async Task<HealthConfirmationResult> CheckAsync(
        Func<ProbeTarget, CancellationToken, Task<ProbeObservation>> check, Action invalidate,
        CancellationToken cancellationToken, Func<CancellationToken, Task>? pause = null,
        Func<CancellationToken, Task<EstablishedObservation>>? established = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // The open connection is exercised on every check so it stays alive; its answer only matters on failure.
        var warm = ObserveEstablishedAsync(established, cancellationToken);
        var primary = await check(ProbeTarget.Gstatic, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (primary.Passed)
        {
            await warm;
            cancellationToken.ThrowIfCancellationRequested();
            return new(primary);
        }
        invalidate();
        await (pause?.Invoke(cancellationToken) ?? Task.Delay(TimeSpan.FromSeconds(1), cancellationToken));
        var repeat = check(ProbeTarget.Gstatic, cancellationToken);
        var alternate = check(ProbeTarget.Slx, cancellationToken);
        var existing = ConfirmEstablishedAsync(warm, established, cancellationToken);
        await Task.WhenAll(repeat, alternate, existing);
        cancellationToken.ThrowIfCancellationRequested();
        return new(primary, await repeat, await alternate, await existing);
    }

    private static async Task<EstablishedObservation?> ConfirmEstablishedAsync(Task<EstablishedObservation?> warm,
        Func<CancellationToken, Task<EstablishedObservation>>? observe, CancellationToken cancellationToken)
    {
        var first = await warm;
        // A stream opened during the failed round cannot establish continuity with the previous round.
        return first is { Reused: true, Observation.Passed: true }
            ? await ObserveEstablishedAsync(observe, cancellationToken)
            : first;
    }

    private static async Task<EstablishedObservation?> ObserveEstablishedAsync(
        Func<CancellationToken, Task<EstablishedObservation>>? observe, CancellationToken cancellationToken)
    {
        if (observe is null) return null;
        try { return await observe(cancellationToken); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return null; }
        catch (Exception error) when (error is HttpRequestException or IOException or ObjectDisposedException) { return null; }
    }
}
