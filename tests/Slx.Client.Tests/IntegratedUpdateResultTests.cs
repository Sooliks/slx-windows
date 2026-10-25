using Slx.Client.Windows;
using Xunit;

namespace Slx.Client.Tests;

public sealed class IntegratedUpdateResultTests
{
    [Fact]
    public void PendingSelectionRetainsIncompleteLatestOutcomeWithoutReplayingAnything()
    {
        var older = new IntegratedUpdateResult(Guid.NewGuid(), "0.1.0-alpha.25", IntegratedUpdatePhase.Succeeded, DateTimeOffset.UtcNow.AddMinutes(-1));
        var incomplete = older with { SessionId = Guid.NewGuid(), Phase = IntegratedUpdatePhase.Installing, UpdatedAt = older.UpdatedAt.AddSeconds(1) };
        Assert.Equal(incomplete, IntegratedUpdateResults.LatestPending([older, null, incomplete]));
        Assert.Null(IntegratedUpdateResults.LatestPending([null]));
        Assert.False(IntegratedUpdatePolicy.CanResume(incomplete with { ResumeConnection = true, ResumeFingerprint = new string('A', 64) },
            incomplete.Version, new string('A', 64)));
    }
    [Theory]
    [InlineData(-300, true)]
    [InlineData(-301, false)]
    [InlineData(60, true)]
    [InlineData(61, false)]
    [InlineData(-86400, false)]
    public void ResumeExpiresAfterFiveMinutesAndRejectsFutureResult(int seconds, bool expected)
    {
        var now = new DateTimeOffset(2026, 10, 25, 18, 0, 0, TimeSpan.Zero); var fingerprint = new string('A', 64);
        var result = new IntegratedUpdateResult(Guid.NewGuid(), "0.1.0-alpha.25", IntegratedUpdatePhase.Succeeded, now.AddSeconds(seconds),
            ResumeConnection: true, ResumeFingerprint: fingerprint, SourceVersion: "0.1.0-alpha.24");
        Assert.Equal(expected, IntegratedUpdatePolicy.CanResumeAt(result, result.Version, fingerprint, now));
        Assert.Equal(expected, IntegratedUpdatePolicy.CanResumeAt(result with { Phase = IntegratedUpdatePhase.Cancelled }, result.SourceVersion!, fingerprint, now));
    }
    [Fact]
    public void KnownRebootRequiredReceiptSurvivesSeveralDaysButNeedsItsOriginalUtcBootStamp()
    {
        var now = new DateTimeOffset(2026, 10, 29, 18, 0, 0, TimeSpan.Zero);
        var old = new IntegratedUpdateResult(Guid.NewGuid(), "0.1.0-alpha.25", IntegratedUpdatePhase.RestartRequired, now.AddDays(-3),
            InstallerExitCode: 3010, BootStartedAt: now.AddDays(-4));
        Assert.True(IntegratedUpdateResults.WithinRetention(old, now));
        Assert.False(IntegratedUpdateResults.WithinRetention(old with { BootStartedAt = null }, now));
        Assert.False(IntegratedUpdateResults.WithinRetention(old with { BootStartedAt = now.AddDays(-2) }, now));
        Assert.False(IntegratedUpdateResults.WithinRetention(old with { BootStartedAt = now.AddDays(-4).ToOffset(TimeSpan.FromHours(3)) }, now));
        Assert.False(IntegratedUpdateResults.WithinRetention(old with { InstallerExitCode = 0 }, now));
        Assert.False(IntegratedUpdateResults.WithinRetention(old with { Phase = IntegratedUpdatePhase.Succeeded }, now));
        Assert.False(IntegratedUpdateResults.WithinRetention(old with { UpdatedAt = now.AddMinutes(2) }, now));
    }
}
