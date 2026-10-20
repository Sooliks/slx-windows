using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Slx.Client.Core;
using Slx.Client.Desktop;
using Slx.Client.Desktop.ViewModels;
using Xunit;
namespace Slx.Client.UiTests;
internal static class ConnectionFactsChecks
{
    public static void Run(MainWindow window, MainViewModel model)
    {
        window.Width = 1200; window.Height = 820;
        using (var empty = new MainViewModel(false))
        {
            window.DataContext = empty; Sta.Pump();
            Assert.False(empty.HasSubscription); AssertHidden(window, empty);
            UiScreenshots.Save(window, "slx-facts-empty");
        }
        window.DataContext = model; Sta.Pump();
        var now = DateTimeOffset.UtcNow;
        foreach (var phase in new[] { TunnelPhase.Disconnected, TunnelPhase.Connecting, TunnelPhase.Reconnecting, TunnelPhase.Stopping, TunnelPhase.Faulted })
        {
            Observe(model, new(phase, now, ProfileName: UiFixtures.Primary.Name, ConnectedAt: now.AddMinutes(-2), ProbePassed: true));
            AssertHidden(window, model);
            UiScreenshots.Save(window, "slx-facts-hidden-" + phase.ToString().ToLowerInvariant());
        }
        Observe(model, new(TunnelPhase.Connected, now, ConnectedAt: now.AddMinutes(-2), ProbePassed: false));
        AssertHidden(window, model);
        Observe(model, new(TunnelPhase.Connected, now, ProfileName: UiFixtures.Primary.Name, ProbePassed: true));
        AssertHidden(window, model);
        Observe(model, new(TunnelPhase.Connected, now, ProfileName: UiFixtures.Primary.Name, ConnectedAt: now.AddMinutes(-2), ProbePassed: true));
        Assert.True(model.HasConnectionFacts); Assert.True(model.HasActiveProtocol);
        Assert.Equal(Visibility.Visible, Facts(window).Visibility);
        Assert.Equal("Hysteria2", model.ActiveProtocol); Assert.Matches(@"^00:02:\d{2}$", model.ConnectionDuration);
        Assert.Equal(now.ToLocalTime().ToString("HH:mm:ss"), model.LastConnectionCheck);
        UiScreenshots.Save(window, "slx-facts-connected-wide");
        window.Width = 940; window.Height = 660; Sta.Pump();
        var scroll = Sta.Descendants<ScrollViewer>(window).Single(item => item.Name == "ConnectionScroll");
        scroll.ScrollToBottom(); Sta.Pump();
        UiScreenshots.Save(window, "slx-facts-connected-compact");
        scroll.ScrollToTop(); Sta.Pump();
        Observe(model, new(TunnelPhase.Connected, now, ProfileName: "Unrecognised profile", ConnectedAt: now.AddMinutes(-2), ProbePassed: true));
        Assert.True(model.HasConnectionFacts); Assert.False(model.HasActiveProtocol); Assert.Empty(model.ActiveProtocol);
        Assert.Equal(Visibility.Collapsed, Sta.Descendants<Border>(Facts(window)).Single(item => item.Name == "ActiveProtocolFact").Visibility);
        var grid = Sta.Descendants<UniformGrid>(Facts(window)).Single();
        var cells = grid.Children.OfType<FrameworkElement>().Where(item => item.IsVisible).ToArray();
        Assert.Equal(2, cells.Length);
        Assert.All(cells, cell => Assert.InRange(Math.Abs(cell.ActualWidth * 2 - grid.ActualWidth), 0, 1));
        scroll.ScrollToBottom(); Sta.Pump();
        UiScreenshots.Save(window, "slx-facts-connected-protocol-unavailable");
        scroll.ScrollToTop(); Sta.Pump();
        model.SendOverride = (_, _) => Task.FromResult(new PipeReply(false, "SERVICE_TIMEOUT", new(TunnelPhase.Disconnected, now)));
        model.PollAsync().GetAwaiter().GetResult(); Sta.Pump();
        AssertHidden(window, model); UiScreenshots.Save(window, "slx-facts-hidden-unknown");
        Observe(model, new(TunnelPhase.Disconnected, now)); AssertHidden(window, model);
        window.Width = 1200; window.Height = 820; Sta.Pump();
        UiScreenshots.Save(window, "slx-facts-off");
    }
    private static StackPanel Facts(MainWindow window) => Sta.Descendants<StackPanel>(window).Single(item => item.Name == "ConnectionFacts");
    private static void AssertHidden(MainWindow window, MainViewModel model)
    {
        Assert.False(model.HasConnectionFacts); Assert.False(model.HasActiveProtocol);
        Assert.Equal(Visibility.Collapsed, Facts(window).Visibility);
        Assert.Empty(model.ConnectionDuration); Assert.Empty(model.ActiveProtocol); Assert.Empty(model.LastConnectionCheck);
    }
    private static void Observe(MainViewModel model, TunnelStatus status)
    {
        model.SendOverride = (_, _) => Task.FromResult(new PipeReply(true, "OK", status));
        model.PollAsync().GetAwaiter().GetResult(); Sta.Pump();
    }
}
