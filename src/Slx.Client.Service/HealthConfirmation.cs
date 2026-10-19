using Slx.Client.Core;
namespace Slx.Client.Service;
internal sealed record HealthConfirmationResult(ProbeObservation Primary, ProbeObservation? Repeat = null,
    ProbeObservation? Alternate = null)
{
    public bool Passed => Primary.Passed || Repeat?.Passed == true || Alternate?.Passed == true;
}
internal static class HealthConfirmation
{
    internal static async Task<HealthConfirmationResult> CheckAsync(
        Func<ProbeTarget, CancellationToken, Task<ProbeObservation>> check, Action invalidate,
        CancellationToken cancellationToken, Func<CancellationToken, Task>? pause = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var primary = await check(ProbeTarget.Gstatic, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (primary.Passed) return new(primary);
        invalidate();
        await (pause?.Invoke(cancellationToken) ?? Task.Delay(TimeSpan.FromSeconds(1), cancellationToken));
        var repeat = check(ProbeTarget.Gstatic, cancellationToken);
        var alternate = check(ProbeTarget.Slx, cancellationToken);
        await Task.WhenAll(repeat, alternate);
        cancellationToken.ThrowIfCancellationRequested();
        return new(primary, await repeat, await alternate);
    }
}
