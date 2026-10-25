using System.Windows;
using System.Windows.Controls;
using Slx.Client.Core;
using Slx.Client.Desktop;
using Slx.Client.Desktop.Localization;
using Slx.Client.Desktop.Presentation;
using Slx.Client.Desktop.ViewModels;
using Xunit;
namespace Slx.Client.UiTests;
internal static class CheckingConnectionChecks
{
    public static void Run(MainWindow window)
    {
        var previous = window.DataContext;
        using var model = new MainViewModel(false, UiFixtures.Subscription,
            UiFixtures.Preferences(RoutingMode.SelectedApplications));
        var connectedAt = DateTimeOffset.UtcNow.AddMinutes(-10);
        var observed = new TunnelStatus(TunnelPhase.Connected, DateTimeOffset.UtcNow, UiFixtures.Primary.Name,
            ConnectedAt: connectedAt, ProbePassed: true, ProtectionActive: true);
        var requests = new List<string>();
        model.SendOverride = (request, _) =>
        {
            requests.Add(request.Action);
            return Task.FromResult(new PipeReply(true, "OK", request.Action == "disconnect"
                ? new(TunnelPhase.Disconnected, DateTimeOffset.UtcNow) : observed));
        };
        window.DataContext = model;
        try
        {
            model.Page = "connection";
            foreach (var theme in new[] { "olive", "terminal" })
            foreach (var language in new[] { "ru", "en" })
            {
                model.Theme = theme; model.Language = language;
                Sta.Resize(window, 940, 660);
                observed = observed with { Phase = TunnelPhase.Connected, ProbePassed = true, ProtectionActive = true };
                Poll(model);
                Assert.True(model.IsConnected); Assert.Equal("connected", model.StatusTone);
                var duration = TimeSpan.Parse(model.ConnectionDuration);
                observed = observed with { Phase = TunnelPhase.Checking, ProbePassed = false };
                Poll(model);
                Assert.False(model.IsConnected); Assert.True(model.IsTunnelActive);
                Assert.Equal("active", model.StatusTone);
                Assert.Equal(Text.Catalog["CheckingConnection"], model.StatusText);
                Assert.Equal(Text.Catalog["CheckingConnectionProtected"], model.StatusDetail);
                Assert.True(model.HasConnectionFacts); Assert.Equal("Hysteria2", model.ActiveProtocol);
                Assert.True(TimeSpan.Parse(model.ConnectionDuration) >= duration);
                Assert.Equal(Visibility.Visible, Named<StackPanel>(window, "ConnectionFacts").Visibility);
                Assert.Contains($"Connected UTC: {connectedAt:O}", model.SafeDiagnostics);
                Assert.True(model.CanConnect); Assert.True(model.ShowConnectionAction);
                var action = Named<Button>(window, "ConnectAction");
                Assert.True(action.IsEnabled); Assert.True(action.IsVisible);
                Assert.Equal(Text.Catalog["Disconnect"], action.Content);
                var detail = Assert.Single(Sta.Descendants<TextBlock>(window), block => block.Text == model.StatusDetail);
                AssertReadableDetail(detail);
                UiScreenshots.Save(window, $"slx-health-checking-{language}-{theme}-compact");
                observed = observed with { Phase = TunnelPhase.Reconnecting };
                Poll(model);
                Assert.Equal(Text.Catalog["Reconnecting"], model.StatusText);
                Assert.NotEqual(Text.Catalog["CheckingConnection"], model.StatusText);
                Assert.False(model.HasConnectionFacts); Assert.False(model.IsConnected);
                observed = observed with { Phase = TunnelPhase.Connected, ProbePassed = true };
                Poll(model);
                Assert.True(model.IsConnected); Assert.Equal("connected", model.StatusTone);
                Assert.True(TimeSpan.Parse(model.ConnectionDuration) >= duration);
            }
            // An unexpected unprotected snapshot cannot claim protection or successful connectivity.
            observed = observed with { Phase = TunnelPhase.Checking, ProtectionActive = false, ProbePassed = true };
            Poll(model);
            Assert.False(model.IsConnected); Assert.False(model.HasConnectionFacts);
            Assert.Equal(Text.Catalog["CheckingConnectionUnconfirmed"], model.StatusDetail);
            Assert.Equal(Text.Catalog["Disconnect"], model.ConnectionAction);
            requests.Clear();
            model.ToggleConnectionCommand.ExecuteAsync(null).GetAwaiter().GetResult();
            Assert.Equal("disconnect", Assert.Single(requests));
            Assert.False(model.IsTunnelActive); Assert.Equal(Text.Catalog["Connect"], model.ConnectionAction);
            CheckingRoutingChecks.Run();
        }
        finally
        {
            window.DataContext = previous;
            Text.Catalog.SetLanguage("ru"); ThemeManager.Apply("olive");
            Sta.Resize(window, 1200, 820);
        }
    }
    private static void AssertReadableDetail(TextBlock detail)
    {
        var measured = new TextBlock
        {
            Text = detail.Text, FontFamily = detail.FontFamily, FontSize = detail.FontSize,
            FontStyle = detail.FontStyle, FontWeight = detail.FontWeight, FontStretch = detail.FontStretch,
            FlowDirection = detail.FlowDirection, TextWrapping = detail.TextWrapping, Padding = detail.Padding,
            LineHeight = detail.LineHeight, LineStackingStrategy = detail.LineStackingStrategy
        };
        Assert.True(detail.ActualWidth > 0);
        measured.Measure(new Size(detail.ActualWidth, double.PositiveInfinity));
        Assert.InRange(measured.DesiredSize.Height, 0, detail.ActualHeight + .1);
    }
    private static void Poll(MainViewModel model) { model.PollAsync().GetAwaiter().GetResult(); Sta.Pump(); }
    private static T Named<T>(DependencyObject root, string name) where T : FrameworkElement =>
        Sta.Descendants<T>(root).Single(item => item.Name == name);
}
