using Slx.Client.Core;
using Slx.Client.Service;
using Xunit;
namespace Slx.Client.Tests;
public sealed class HealthConfirmationTests
{
    [Fact]
    public async Task FirstSuccessDoesNotInvalidateOrProbeOtherOrigins()
    {
        var calls = new List<ProbeTarget>();
        var result = await HealthConfirmation.CheckAsync((target, _) =>
        { calls.Add(target); return Task.FromResult(new ProbeObservation(target, ProbeOutcome.Success, 1)); },
            () => throw new InvalidOperationException("Unexpected invalidation"), default);
        Assert.True(result.Passed); Assert.Equal(new[] { ProbeTarget.Gstatic }, calls);
        Assert.Null(result.Repeat); Assert.Null(result.Alternate);
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    [InlineData(false, false, false)]
    public async Task FailureRequiresIndependentConfirmation(bool repeated, bool alternate, bool passed)
    {
        bool invalidated = false, paused = false; int calls = 0;
        var result = await HealthConfirmation.CheckAsync((target, _) =>
        {
            calls++;
            if (calls == 1) return Task.FromResult(new ProbeObservation(target, ProbeOutcome.Timeout, 5000));
            Assert.True(invalidated); Assert.True(paused);
            return Task.FromResult(new ProbeObservation(target,
                (target == ProbeTarget.Gstatic ? repeated : alternate) ? ProbeOutcome.Success : ProbeOutcome.Connection, 2));
        }, () => invalidated = true, default, _ => { paused = true; return Task.CompletedTask; });
        Assert.Equal(3, calls); Assert.Equal(passed, result.Passed);
        Assert.Equal(ProbeTarget.Gstatic, result.Repeat!.Target);
        Assert.Equal(ProbeTarget.Slx, result.Alternate!.Target);
        Assert.Equal(ProbeOutcome.Timeout, result.Primary.Outcome);
    }

    [Fact]
    public async Task CallerCancellationIsNeverConvertedToHealthy()
    {
        using var cancel = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => HealthConfirmation.CheckAsync(
            (target, _) => Task.FromResult(new ProbeObservation(target, ProbeOutcome.Timeout, 1)),
            () => cancel.Cancel(), cancel.Token));
    }

    [Fact]
    public async Task CancellationRacingSuccessfulResponseDoesNotRestoreSuccess()
    {
        using var cancel = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => HealthConfirmation.CheckAsync(
            (target, _) => { cancel.Cancel(); return Task.FromResult(new ProbeObservation(target, ProbeOutcome.Success, 1)); },
            () => throw new InvalidOperationException(), cancel.Token));
    }
    [Fact]
    public void DiagnosticsRetainFailedIncidentWithoutInventingSuccess()
    {
        var engine = new TunnelEngine();
        var failed = new ProbeObservation(ProbeTarget.Gstatic, ProbeOutcome.Timeout, 5000);
        var success = new ProbeObservation(ProbeTarget.Slx, ProbeOutcome.Success, 10, 200);
        engine.RecordHealthIncident(new(failed, failed, success), true);
        engine.RecordHealthIncident(new(failed, failed, failed), false);
        engine.RecordHealthIncident(new(success), true);
        var status = engine.Status;
        Assert.Equal(2, status.HealthCheck!.Incidents);
        Assert.Equal(1, status.HealthCheck.RetainedConnections);
        Assert.False(status.ProbePassed); Assert.Null(status.ConnectedAt); Assert.Null(status.Recovery);
        Assert.Equal(TunnelPhase.Disconnected, status.Phase);
        Assert.Equal(status, System.Text.Json.JsonSerializer.Deserialize<TunnelStatus>(System.Text.Json.JsonSerializer.Serialize(status)));
    }
}
