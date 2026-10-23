using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Slx.Client.Desktop.Presentation;
using Xunit;
namespace Slx.Client.UiTests;
internal static class MotionLifecycleChecks
{
    public static void Run()
    {
        var suppressed = Motion.Suppressed;
        var preference = Motion.SystemPreferenceOverride;
        var probe = new Border { Width = 20, Height = 20 };
        var host = new Window { Content = probe, Width = 420, Height = 320, ShowInTaskbar = false, WindowStyle = WindowStyle.None, Left = -20000, Top = -20000 };
        Motion.Suppressed = false; Motion.SystemPreferenceOverride = () => true;
        host.Show(); Sta.Pump();
        try
        {
            Arm(probe); host.Hide(); Sta.Pump(); AssertStopped(probe);
            host.Show(); Sta.Pump();
            Arm(probe); host.WindowState = WindowState.Minimized; Sta.Pump(); AssertStopped(probe);
            host.WindowState = WindowState.Normal; Sta.Pump();
            Arm(probe); host.Content = null; Sta.Pump(); AssertStopped(probe);
            host.Content = probe; Sta.Pump();
            Arm(probe); Motion.SystemPreferenceOverride = () => false; AssertStopped(probe);
            Motion.SystemPreferenceOverride = () => true;
            Arm(probe); Motion.Suppressed = true; AssertStopped(probe);
            Motion.Suppressed = false;
            Arm(probe); WaitForStop(probe); AssertStopped(probe);
            Motion.Enter(probe); Assert.True(MotionLifetime.IsTracking(probe));
            host.Hide(); Sta.Pump(); AssertStopped(probe);
            var shift = Assert.IsType<TranslateTransform>(probe.RenderTransform);
            Assert.Equal(0, shift.Y); Assert.False(shift.HasAnimatedProperties);
            host.Show(); Sta.Pump();
            CheckConfirmation(host);
            CheckRoute(host);
        }
        finally
        {
            host.Close(); Sta.Pump(); Motion.SystemPreferenceOverride = preference; Motion.Suppressed = suppressed;
        }
    }
    private static void Arm(Border probe)
    {
        Motion.Suppressed = true; Motion.SetFadeTo(probe, 0); Motion.Suppressed = false;
        Motion.SetFadeTo(probe, 0.75);
        Assert.True(probe.HasAnimatedProperties); Assert.True(MotionLifetime.IsTracking(probe));
        Assert.Equal(0.75, (double)probe.GetAnimationBaseValue(UIElement.OpacityProperty), 3);
    }
    private static void AssertStopped(Border probe)
    {
        Assert.False(probe.HasAnimatedProperties); Assert.False(MotionLifetime.IsTracking(probe));
        Assert.Equal(0.75, probe.Opacity, 3);
    }
    private static void CheckConfirmation(Window host)
    {
        var ring = new Border { Width = 8, Height = 8, Opacity = 0 };
        host.Content = ring; Sta.Pump();
        Motion.SetConfirmWhen(ring, true); Assert.True(MotionLifetime.IsTracking(ring));
        Motion.SetConfirmWhen(ring, false);
        Assert.False(ring.HasAnimatedProperties); Assert.False(MotionLifetime.IsTracking(ring)); Assert.Equal(0, ring.Opacity);
        Assert.False(Assert.IsType<ScaleTransform>(ring.RenderTransform).HasAnimatedProperties);
    }
    private static void CheckRoute(Window host)
    {
        var scene = new AmbientBackdrop(); host.Content = scene; Sta.Pump();
        scene.RoutedApps = 2; Assert.True(scene.HasAnimatedProperties); Assert.True(MotionLifetime.IsTracking(scene));
        Assert.Equal(1, (double)scene.GetAnimationBaseValue(AmbientBackdrop.ProgressProperty));
        host.Hide(); Sta.Pump(); AssertRouteStopped(scene);
        host.Show(); Sta.Pump(); scene.RoutedApps = 4;
        Assert.True(scene.HasAnimatedProperties); WaitForStop(scene); AssertRouteStopped(scene);
        scene.RoutedApps = 5; Assert.True(scene.HasAnimatedProperties);
        Motion.SystemPreferenceOverride = () => false; AssertRouteStopped(scene);
        Motion.SystemPreferenceOverride = () => true;
        scene.WholeRoute = true; Assert.True(scene.HasAnimatedProperties);
        host.WindowState = WindowState.Minimized; Sta.Pump(); AssertRouteStopped(scene);
        host.WindowState = WindowState.Normal; Sta.Pump();
        scene.WholeRoute = false; Assert.True(scene.HasAnimatedProperties);
        host.Content = null; Sta.Pump(); AssertRouteStopped(scene);
    }
    private static void AssertRouteStopped(AmbientBackdrop scene)
    {
        Assert.Equal(1, scene.Progress); Assert.Equal(1, (double)scene.GetAnimationBaseValue(AmbientBackdrop.ProgressProperty));
        Assert.False(scene.HasAnimatedProperties); Assert.False(MotionLifetime.IsTracking(scene));
    }
    private static void WaitForStop(UIElement element)
    {
        var frame = new DispatcherFrame(); var elapsed = Stopwatch.StartNew();
        var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(10) };
        EventHandler tick = (_, _) => { if (!MotionLifetime.IsTracking(element) || elapsed.Elapsed > TimeSpan.FromSeconds(3)) frame.Continue = false; };
        timer.Tick += tick; timer.Start();
        try { Dispatcher.PushFrame(frame); }
        finally { timer.Stop(); timer.Tick -= tick; }
        Assert.False(MotionLifetime.IsTracking(element), "One-shot clock did not detach after its finite duration");
    }
}
