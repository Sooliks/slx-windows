using System.Windows.Controls;
using Slx.Client.Core;
using Slx.Client.Desktop;
using Slx.Client.Desktop.Localization;
using Slx.Client.Desktop.Presentation;
using Slx.Client.Desktop.ViewModels;
using Xunit;
namespace Slx.Client.UiTests;
internal static class TrafficRouteDiagnosticsChecks
{
    public static void Run(MainWindow window)
    {
        var previous = window.DataContext;
        using var model = new MainViewModel(false, UiFixtures.Subscription,
            UiFixtures.Preferences(RoutingMode.SelectedApplications));
        var routes = new TrafficRouteCheckObservation(DateTimeOffset.UtcNow,
            new(DnsRouteStatus.OtherInterface, PrefixLength: 2, RouteMetric: 0),
            new(DnsRouteStatus.OwnedTunnel, PrefixLength: 1, RouteMetric: 0));
        var observed = new TunnelStatus(TunnelPhase.Disconnected, DateTimeOffset.UtcNow,
            Code: "VPN_TRAFFIC_ROUTE_FAILED", TrafficRoutes: routes);
        model.SendOverride = (_, _) => Task.FromResult(new PipeReply(true, "OK", observed));
        try
        {
            window.DataContext = model; model.Page = "connection";
            model.PollAsync().GetAwaiter().GetResult();
            Assert.False(model.IsConnected);
            Assert.Equal("VPN_TRAFFIC_ROUTE_FAILED", model.LastServiceCode);
            Assert.Contains("Traffic route IPv4 lower: OtherInterface, prefix_length=2, route_metric=0", model.SafeDiagnostics);
            Assert.Contains("Traffic route IPv4 upper: OwnedTunnel", model.SafeDiagnostics);
            Assert.Contains("Traffic route IPv6: not checked", model.SafeDiagnostics);
            Assert.DoesNotContain("8.8.8.8", model.SafeDiagnostics);
            Assert.DoesNotContain(UiFixtures.Primary.Host, model.SafeDiagnostics);
            Assert.DoesNotContain(UiFixtures.Primary.Name, model.SafeDiagnostics);
            foreach (var language in new[] { "ru", "en" })
            {
                model.Language = language; Sta.Resize(window, 940, 660);
                Assert.Equal(Text.Catalog["FaultVpnTrafficRoute"], model.StatusDetail);
                Assert.True(ServiceStateCopy.IsDefinitiveConnectionFault("VPN_TRAFFIC_ROUTE_FAILED"));
                var scroll = Sta.Descendants<ScrollViewer>(window).Single(item => item.Name == "ConnectionScroll");
                Assert.True(scroll.ExtentWidth <= scroll.ViewportWidth + 1);
                UiScreenshots.Save(window, "slx-data-route-failed-" + language);
            }
        }
        finally
        {
            window.DataContext = previous;
            if (previous is MainViewModel original) { Text.Catalog.SetLanguage(original.Language); ThemeManager.Apply(original.Theme); }
            Sta.Pump();
        }
    }
}
