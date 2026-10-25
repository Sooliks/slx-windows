using Slx.Client.Core;
using Slx.Client.Windows;
using Xunit;
namespace Slx.Client.Tests;

public sealed class IntegratedUpdateRebootTests
{
    private static readonly DateTimeOffset Before = DateTimeOffset.Parse("2026-10-25T10:00:00Z");
    private static IntegratedUpdateResult Result(Guid id) => new(id, "0.1.0-alpha.25", IntegratedUpdatePhase.RestartRequired,
        Before.AddHours(1), 3010, true, new string('A', 64), "0.1.0-alpha.24", Before);
    [Fact]
    public void BootObservationRejectsMalformedNonUtcAndSameBootWithinTolerance()
    {
        Assert.Equal(Before, IntegratedUpdateBoot.Parse(Before.ToString("O")));
        Assert.Null(IntegratedUpdateBoot.Parse("not a boot time"));
        Assert.Null(IntegratedUpdateBoot.Parse(Before.ToOffset(TimeSpan.FromHours(3)).ToString("O")));
        Assert.Null(IntegratedUpdateBoot.Parse(new string('x', 257)));
        Assert.False(IntegratedUpdateBoot.Different(null, Before));
        Assert.False(IntegratedUpdateBoot.Different(Before, null));
        Assert.False(IntegratedUpdateBoot.Different(Before, Before.AddMilliseconds(999)));
        Assert.True(IntegratedUpdateBoot.Different(Before, Before.AddMinutes(1)));
    }
    [Fact]
    public async Task KnownChangedBootRequiresMatchingBinariesAndFreshClaimBeforeExactRelease()
    {
        var id = Guid.NewGuid(); int calls = 0;
        Task<PipeReply> Send(PipeRequest request, CancellationToken token)
        {
            calls++; Assert.Null(request.Plan);
            if (calls == 1)
            {
                Assert.Equal("status", request.Action);
                return Task.FromResult(new PipeReply(true, "OK", new(TunnelPhase.Disconnected, DateTimeOffset.UtcNow,
                    ServiceVersion: "0.1.0-alpha.25", Code: "UPDATE_MAINTENANCE_UNCONFIRMED",
                    UpdateMaintenance: new(id, DateTimeOffset.UtcNow.AddMinutes(-1), true))));
            }
            Assert.Equal("release-update", request.Action); Assert.Equal(id, request.UpdateSessionId);
            return Task.FromResult(new PipeReply(true, "OK", new(TunnelPhase.Disconnected, DateTimeOffset.UtcNow,
                ServiceVersion: "0.1.0-alpha.25")));
        }
        Assert.True(await IntegratedUpdateReboot.TryCompleteAsync(Result(id), "0.1.0-alpha.25",
            _ => Task.FromResult<DateTimeOffset?>(Before.AddHours(2)), () => true, Send));
        Assert.Equal(2, calls); // No connect, installer retry or restart action is possible through this helper.
    }
    [Fact]
    public async Task SameUnknownBootWrongVersionAndUnconfirmedInstallationNeverRelease()
    {
        var id = Guid.NewGuid(); int sends = 0;
        Task<PipeReply> Send(PipeRequest request, CancellationToken token)
        { sends++; throw new InvalidOperationException(); }
        foreach (var boot in new DateTimeOffset?[] { null, Before, Before.AddSeconds(1) })
            Assert.False(await IntegratedUpdateReboot.TryCompleteAsync(Result(id), "0.1.0-alpha.25",
                _ => Task.FromResult(boot), () => true, Send));
        Assert.False(await IntegratedUpdateReboot.TryCompleteAsync(Result(id), "0.1.0-alpha.24",
            _ => Task.FromResult<DateTimeOffset?>(Before.AddHours(2)), () => true, Send));
        Assert.False(await IntegratedUpdateReboot.TryCompleteAsync(Result(id) with { InstallerExitCode = 0 }, "0.1.0-alpha.25",
            _ => Task.FromResult<DateTimeOffset?>(Before.AddHours(2)), () => true, Send));
        Assert.False(await IntegratedUpdateReboot.TryCompleteAsync(Result(id), "0.1.0-alpha.25",
            _ => Task.FromResult<DateTimeOffset?>(Before.AddHours(2)), () => false, Send));
        Assert.Equal(0, sends);
    }
    [Theory]
    [InlineData(TunnelPhase.Connected, false, true)]
    [InlineData(TunnelPhase.Disconnected, true, true)]
    [InlineData(TunnelPhase.Disconnected, false, false)]
    public async Task LiveOrUnknownServiceNeverReleasesAfterChangedBoot(TunnelPhase phase, bool protection, bool sameSession)
    {
        var id = Guid.NewGuid(); int sends = 0;
        Assert.False(await IntegratedUpdateReboot.TryCompleteAsync(Result(id), "0.1.0-alpha.25",
            _ => Task.FromResult<DateTimeOffset?>(Before.AddHours(2)), () => true, (request, _) =>
            {
                sends++; Assert.Equal("status", request.Action);
                return Task.FromResult(new PipeReply(true, "OK", new(phase, DateTimeOffset.UtcNow, ProtectionActive: protection,
                    ServiceVersion: "0.1.0-alpha.25", UpdateMaintenance: new(sameSession ? id : Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(5), true))));
            }));
        Assert.Equal(1, sends);
    }
}
