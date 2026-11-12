using Slx.Client.Service;
using Xunit;

namespace Slx.Client.Tests;

public sealed class UpstreamStallHoldTests
{
    [Fact]
    public void RepeatedFailuresCannotExtendTheFortyFiveSecondMonotonicWindow()
    {
        long now = 1000; var grace = new UpstreamStallHold(() => now, 10);
        Assert.False(grace.Pending); Assert.True(grace.Hold()); Assert.True(grace.Pending);
        now += 449; Assert.True(grace.Hold()); Assert.Equal(TimeSpan.FromMilliseconds(100), grace.Remaining);
        now++; Assert.False(grace.Hold()); Assert.Equal(TimeSpan.Zero, grace.Remaining);
        now += 1000; Assert.False(grace.Hold()); Assert.Equal(2, grace.Holds);
        grace.Reset(); Assert.False(grace.Pending); Assert.True(grace.Hold());
    }
    [Fact]
    public void BackwardClockCannotExtendGrace()
    {
        long now = 100; var grace = new UpstreamStallHold(() => now, 1);
        Assert.True(grace.Hold()); now--;
        Assert.False(grace.Hold()); Assert.Equal(TimeSpan.Zero, grace.Remaining);
    }
    [Fact]
    public void ExplicitOffClearsGraceBeforeAnyPotentiallyFailingCleanup()
    {
        var engine = new TunnelEngine();
        var field = typeof(TunnelEngine).GetField("upstreamHold", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var grace = (UpstreamStallHold)field.GetValue(engine)!; Assert.True(grace.Hold());
        typeof(TunnelEngine).GetMethod("RequestOff", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(engine, [null]);
        Assert.False(grace.Pending);
    }
}
