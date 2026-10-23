using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Slx.Client.Core;
using Slx.Client.Desktop;
using Slx.Client.Desktop.Presentation;
using Slx.Client.Desktop.ViewModels;
using Xunit;
namespace Slx.Client.UiTests;
internal static class MotionChecks
{
    public static void Run(MainWindow window, MainViewModel model)
    {
        CheckPolicy();
        MotionLifecycleChecks.Run();
        CheckRouteScene(window, model);
        CheckScrollMemory(window, model);
    }
    // Motion is decoration: with Windows animations off, a minimized window or a render check, values land synchronously.
    private static void CheckPolicy()
    {
        var probe = new Border { Width = 20, Height = 20 };
        var host = new Window { Content = probe, Width = 80, Height = 80, ShowInTaskbar = false, WindowStyle = WindowStyle.None, Left = -20000, Top = -20000 };
        var suppressed = Motion.Suppressed;
        host.Show(); Sta.Pump();
        try
        {
            Motion.Suppressed = true;
            Motion.SetFadeTo(probe, 0); Motion.SetFadeTo(probe, 0.4);
            Assert.Equal(0.4, probe.Opacity, 3); Assert.False(probe.HasAnimatedProperties);
            Motion.Suppressed = false; Motion.SystemPreferenceOverride = () => true;
            Assert.True(Motion.IsEnabled(probe));
            Motion.SetFadeTo(probe, 1);
            Assert.True(probe.HasAnimatedProperties);
            Assert.Equal(1, (double)probe.GetAnimationBaseValue(UIElement.OpacityProperty), 3);
            Motion.SystemPreferenceOverride = () => false;
            Assert.False(Motion.IsEnabled(probe));
            Motion.SetFadeTo(probe, 0.2);
            Assert.False(probe.HasAnimatedProperties); Assert.Equal(0.2, probe.Opacity, 3);
            Motion.SystemPreferenceOverride = () => true;
            host.WindowState = WindowState.Minimized; Sta.Pump();
            Assert.False(Motion.IsEnabled(probe));
            Motion.SetShiftX(probe, 18);
            var shift = Assert.IsType<TranslateTransform>(probe.RenderTransform);
            Assert.Equal(18, shift.X, 3); Assert.False(shift.HasAnimatedProperties);
            Motion.Enter(probe);
            Assert.False(probe.HasAnimatedProperties); Assert.Equal(0.2, probe.Opacity, 3); Assert.Equal(0, shift.Y, 3);
        }
        finally { Motion.Suppressed = suppressed; Motion.SystemPreferenceOverride = null; host.Close(); Sta.Pump(); }
    }
    // The route scene restates the saved choice only: mode and selected-app count, never connection or traffic.
    private static void CheckRouteScene(MainWindow window, MainViewModel model)
    {
        Assert.All(AmbientBackdrop.Branches(0, true), value => Assert.Equal(1, value));
        Assert.Equal(new double[] { 1, 1, 1, 0, 0, 0, 0, 0 }, AmbientBackdrop.Branches(3, false));
        var many = AmbientBackdrop.Branches(20, false);
        Assert.Equal(6, many.Count(value => value == 1)); Assert.Equal(0, many[6]); Assert.Equal(0, many[7]);
        Assert.All(AmbientBackdrop.Branches(-2, false), value => Assert.Equal(0, value));
        // The window binds the saved choice; toggling the live model here could queue a real routing mutation.
        var bound = Assert.Single(Sta.Descendants<AmbientBackdrop>(window));
        Assert.NotNull(System.Windows.Data.BindingOperations.GetBindingExpression(bound, AmbientBackdrop.RoutedAppsProperty));
        Assert.NotNull(System.Windows.Data.BindingOperations.GetBindingExpression(bound, AmbientBackdrop.WholeRouteProperty));
        Assert.Equal(model.SelectedAppCount, bound.RoutedApps); Assert.Equal(model.IsWholeMode, bound.WholeRoute);
        var scene = new AmbientBackdrop { Width = 400, Height = 300 };
        var host = new Window { Content = scene, Width = 420, Height = 320, ShowInTaskbar = false, WindowStyle = WindowStyle.None, Left = -20000, Top = -20000 };
        host.Show(); Sta.Pump();
        try
        {
            scene.RoutedApps = 4; Assert.Equal(1, scene.Progress, 3); Assert.False(scene.HasAnimatedProperties);
            scene.WholeRoute = true; Assert.Equal(1, scene.Progress, 3); Assert.False(scene.HasAnimatedProperties);
            UiScreenshots.Save(scene, "slx-route-scene-whole");
        }
        finally { host.Close(); Sta.Pump(); }
    }
    private static void CheckScrollMemory(MainWindow window, MainViewModel model)
    {
        window.Width = 940; window.Height = 660; model.Page = "applications"; Sta.Pump();
        var scroll = Page(window, "applications");
        Assert.True(scroll.ScrollableHeight > 60);
        scroll.ScrollToVerticalOffset(120); Sta.Pump();
        var offset = scroll.VerticalOffset; Assert.True(offset > 0);
        model.Page = "connection"; Sta.Pump();
        model.Page = "applications"; Sta.Pump();
        var restored = Page(window, "applications");
        Assert.NotSame(scroll, restored);
        Assert.Equal(offset, restored.VerticalOffset, 1);
        restored.ScrollToTop(); Sta.Pump();
        model.Page = "connection"; window.Width = 1200; window.Height = 820; Sta.Pump();
        Assert.Equal(0, ScrollMemory.Saved(model, "applications") ?? 0, 1);
    }
    private static ScrollViewer Page(Window window, string key) =>
        Sta.Descendants<ScrollViewer>(window).Single(scroll => ScrollMemory.GetKey(scroll) == key);
}
internal static class PresentationStateChecks
{
    public static void Run()
    {
        using var model = new MainViewModel(false, UiFixtures.Subscription, UiFixtures.Preferences(RoutingMode.SelectedApplications));
        var now = DateTimeOffset.UtcNow;
        Assert.Equal("unknown", model.StatusTone);
        Observe(model, new(TunnelPhase.Disconnected, now)); Assert.Equal("off", model.StatusTone);
        Observe(model, new(TunnelPhase.Connecting, now)); Assert.Equal("active", model.StatusTone);
        Observe(model, new(TunnelPhase.Connected, now, ProbePassed: false)); Assert.Equal("active", model.StatusTone);
        Observe(model, new(TunnelPhase.Connected, now, ProbePassed: true)); Assert.Equal("connected", model.StatusTone);
        Observe(model, new(TunnelPhase.Faulted, now, Code: "CORE_FAILED")); Assert.Equal("attention", model.StatusTone);
        Observe(model, new(TunnelPhase.Disconnected, now));
        model.SendOverride = (_, _) => Task.FromResult(new PipeReply(false, "SERVICE_TIMEOUT", new(TunnelPhase.Disconnected, now)));
        model.PollAsync().GetAwaiter().GetResult(); Sta.Pump();
        Assert.Equal("unknown", model.StatusTone);
        // An unconfirmed state keeps the link and explains why instead of leaving a silent open dialog.
        model.AskRemoveCommand.Execute(null); Assert.True(model.RemoveVisible); Assert.False(model.RemoveBlocked);
        model.ConfirmRemoveCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        Assert.True(model.HasSubscription); Assert.True(model.RemoveVisible); Assert.True(model.RemoveBlocked);
        model.CancelOverlayCommand.Execute(null); Assert.False(model.RemoveVisible); Assert.False(model.RemoveBlocked);
        Observe(model, new(TunnelPhase.Disconnected, now));
        model.AskRemoveCommand.Execute(null);
        model.ConfirmRemoveCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        Assert.False(model.HasSubscription); Assert.False(model.RemoveVisible); Assert.False(model.RemoveBlocked);
    }
    private static void Observe(MainViewModel model, TunnelStatus status)
    {
        model.SendOverride = (_, _) => Task.FromResult(new PipeReply(true, "OK", status));
        model.PollAsync().GetAwaiter().GetResult(); Sta.Pump();
    }
}
