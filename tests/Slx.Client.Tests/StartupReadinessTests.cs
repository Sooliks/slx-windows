using Slx.Client.Core;
using Slx.Client.Service;
using Xunit;

namespace Slx.Client.Tests;

public sealed class StartupReadinessTests
{
    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(false, false, false)]
    public async Task EitherVerifiedOriginCanConfirmReadiness(bool gstatic, bool slx, bool expected)
    {
        var calls = new HashSet<ProbeTarget>();
        bool result = await StartupReadiness.CheckAsync((target, _) =>
        {
            calls.Add(target);
            return Task.FromResult(new ProbeObservation(target,
                (target == ProbeTarget.Gstatic ? gstatic : slx) ? ProbeOutcome.Success : ProbeOutcome.Timeout, 1));
        }, default);
        Assert.Equal(expected, result);
        Assert.Equal(2, calls.Count);
    }

    [Fact]
    public async Task BothRequestsStartBeforeEitherCompletesAndCancellationCannotBecomeSuccess()
    {
        using var cancellation = new CancellationTokenSource();
        var response = new TaskCompletionSource<ProbeObservation>(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls = 0;
        var readiness = StartupReadiness.CheckAsync((target, _) =>
        {
            calls++;
            return target == ProbeTarget.Gstatic ? response.Task
                : Task.FromResult(new ProbeObservation(target, ProbeOutcome.Success, 1));
        }, cancellation.Token);
        Assert.Equal(2, calls);
        cancellation.Cancel();
        response.SetResult(new(ProbeTarget.Gstatic, ProbeOutcome.Success, 1));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => readiness);
    }
}
