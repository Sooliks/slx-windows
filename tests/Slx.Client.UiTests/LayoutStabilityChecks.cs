using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Slx.Client.Core;
using Slx.Client.Desktop;
using Slx.Client.Desktop.Localization;
using Slx.Client.Desktop.Presentation;
using Slx.Client.Desktop.ViewModels;
using Xunit;
namespace Slx.Client.UiTests;
// Owner report: "Please wait…" and similar copy appeared for ~0.5 s and made the interface jump.
internal static class LayoutStabilityChecks
{
    public static void Run(MainWindow window)
    {
        var previous = window.DataContext;
        try
        {
            CheckConnectionPage(window);
            CheckPresenceTiming();
        }
        finally { window.DataContext = previous; Text.Catalog.SetLanguage("ru"); Sta.Pump(); }
    }
    private static void CheckConnectionPage(MainWindow window)
    {
        using var model = new MainViewModel(false, UiFixtures.Subscription, UiFixtures.Preferences(RoutingMode.SelectedApplications));
        var observed = new TunnelStatus(TunnelPhase.Disconnected, DateTimeOffset.UtcNow);
        var pending = new TaskCompletionSource<PipeReply>(TaskCreationOptions.RunContinuationsAsynchronously);
        model.SendOverride = (request, _) => request.Action == "status" ? Task.FromResult(new PipeReply(true, "OK", observed)) : pending.Task;
        window.DataContext = model; Sta.Resize(window, 940, 660);
        model.PollAsync().GetAwaiter().GetResult(); Sta.Pump();
        var card = Named<Border>(window, "StatusCard"); var subscription = Named<Border>(window, "SubscriptionSection");
        var hint = Named<TextBlock>(window, "ActivityHint");
        var height = card.ActualHeight; var below = Bounds(subscription, window);
        Assert.Equal(Visibility.Hidden, hint.Visibility);
        // Busy: copy appears in the reserved status line; nothing below the card moves.
        var connect = model.ToggleConnectionCommand.ExecuteAsync(null); Sta.Pump();
        Assert.True(model.IsBusy); Assert.True(hint.IsVisible); Assert.Equal(Text.Catalog["Busy"], hint.Text);
        Assert.Equal(height, card.ActualHeight, 1); Assert.Equal(below, Bounds(subscription, window));
        observed = new(TunnelPhase.Connected, DateTimeOffset.UtcNow, UiFixtures.Primary.Name, ConnectedAt: DateTimeOffset.UtcNow,
            Mode: RoutingMode.SelectedApplications, ProbePassed: true, ProtectionActive: true);
        pending.SetResult(new PipeReply(true, "OK", observed));
        AutoRoutingChecks.Settle(() => connect.IsCompleted && !model.IsBusy);
        Assert.False(hint.IsVisible); Assert.Equal(height, card.ActualHeight, 1); Assert.Equal(below, Bounds(subscription, window));
        // A fault notice floats above the page instead of inserting a banner over it.
        var top = Bounds(card, window).Top;
        model.SendOverride = (_, _) => Task.FromResult(new PipeReply(true, "OK", new(TunnelPhase.Faulted, DateTimeOffset.UtcNow, Code: "CORE_FAILED")));
        model.PollAsync().GetAwaiter().GetResult(); Sta.Pump();
        Assert.True(model.ShowMessage); Assert.True(Named<Grid>(window, "MessageNotice").IsVisible);
        Assert.Equal(top, Bounds(card, window).Top, 1);
        UiScreenshots.Save(window, "slx-floating-notice-ru-narrow");
        model.DismissMessageCommand.Execute(null); Sta.Pump();
        Assert.False(model.ShowMessage); Assert.False(Named<Grid>(window, "MessageNotice").IsVisible);
    }
    // Short activity never flashes; once shown it stays long enough to read. Space is reserved throughout.
    private static void CheckPresenceTiming()
    {
        var probe = new TextBlock { Text = "activity", Visibility = Visibility.Hidden };
        var host = new Window { Content = probe, Width = 200, Height = 80, ShowInTaskbar = false, WindowStyle = WindowStyle.None, Left = -20000, Top = -20000 };
        var suppressed = Motion.Suppressed;
        host.Show(); Sta.Pump();
        try
        {
            Motion.Suppressed = false;
            Presence.SetShow(probe, true); Presence.SetShow(probe, false); Wait(Presence.Delay + TimeSpan.FromMilliseconds(150));
            Assert.Equal(Visibility.Hidden, probe.Visibility);
            Presence.SetShow(probe, true); Assert.Equal(Visibility.Hidden, probe.Visibility);
            Wait(Presence.Delay + TimeSpan.FromMilliseconds(150)); Assert.Equal(Visibility.Visible, probe.Visibility);
            var shown = Stopwatch.StartNew();
            Presence.SetShow(probe, false); Assert.Equal(Visibility.Visible, probe.Visibility);
            Wait(Presence.Minimum + TimeSpan.FromMilliseconds(150));
            Assert.Equal(Visibility.Hidden, probe.Visibility); Assert.True(shown.Elapsed >= Presence.Minimum - Presence.Delay);
            Assert.True(probe.ActualHeight > 0);
        }
        finally { Motion.Suppressed = suppressed; host.Close(); Sta.Pump(); }
    }
    private static void Wait(TimeSpan span)
    {
        var clock = Stopwatch.StartNew();
        while (clock.Elapsed < span) { Thread.Sleep(10); Sta.Pump(); }
    }
    private static T Named<T>(DependencyObject root, string name) where T : FrameworkElement =>
        Sta.Descendants<T>(root).Single(item => item.Name == name);
    private static Rect Bounds(FrameworkElement element, Window window) =>
        element.TransformToAncestor(window).TransformBounds(new Rect(element.RenderSize));
}
