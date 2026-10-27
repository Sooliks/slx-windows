using Slx.Client.Core;
using Slx.Client.Service;
using Xunit;

namespace Slx.Client.Tests;

public sealed class RemoteHealthGraceTests
{
    [Fact]
    public void RepeatedFailuresCannotExtendTheNinetySecondMonotonicWindow()
    {
        long now = 1000; var grace = new RemoteHealthGrace(() => now, 10);
        Assert.False(grace.Pending); Assert.True(grace.Defer(RoutingMode.SelectedApplications)); Assert.True(grace.Pending);
        now += 899; Assert.True(grace.Defer(RoutingMode.SelectedApplications)); Assert.Equal(TimeSpan.FromMilliseconds(100), grace.Remaining);
        now++; Assert.False(grace.Defer(RoutingMode.SelectedApplications)); Assert.Equal(TimeSpan.Zero, grace.Remaining);
        now += 1000; Assert.False(grace.Defer(RoutingMode.SelectedApplications));
        grace.Reset(); Assert.False(grace.Pending); Assert.True(grace.Defer(RoutingMode.SelectedApplications));
    }
    [Fact]
    public void WholeComputerAndBackwardClockCannotAcquireOrExtendGrace()
    {
        long now = 100; var grace = new RemoteHealthGrace(() => now, 1);
        Assert.False(grace.Defer(RoutingMode.WholeComputer)); Assert.False(grace.Pending);
        Assert.True(grace.Defer(RoutingMode.SelectedApplications)); now--;
        Assert.False(grace.Defer(RoutingMode.SelectedApplications)); Assert.Equal(TimeSpan.Zero, grace.Remaining);
        Assert.False(grace.Defer(RoutingMode.WholeComputer)); Assert.False(grace.Pending);
    }
    [Fact]
    public void ExplicitOffClearsGraceBeforeAnyPotentiallyFailingCleanup()
    {
        var engine = new TunnelEngine();
        var field = typeof(TunnelEngine).GetField("remoteHealthGrace", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var grace = (RemoteHealthGrace)field.GetValue(engine)!; Assert.True(grace.Defer(RoutingMode.SelectedApplications));
        typeof(TunnelEngine).GetMethod("RequestOff", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(engine, [null]);
        Assert.False(grace.Pending);
    }
}
