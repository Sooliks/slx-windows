using System.Windows.Controls;
using Slx.Client.Core;
using Slx.Client.Desktop;
using Slx.Client.Desktop.Localization;
using Slx.Client.Desktop.ViewModels;
using Xunit;

namespace Slx.Client.UiTests;

internal static class DnsDiagnosticsChecks
{
    public static void Run(MainWindow window)
    {
        var previous = window.DataContext;
        using var model = new MainViewModel(false, UiFixtures.Subscription,
            UiFixtures.Preferences(RoutingMode.SelectedApplications));
        window.DataContext = model;
        var dns = new DnsCheckObservation(DateTimeOffset.UtcNow,
            new(ProbeTarget.Gstatic, DnsProbeOutcome.Timeout, 3000, Route: new(DnsRouteStatus.OwnedTunnel)),
            new(ProbeTarget.Slx, DnsProbeOutcome.SocketError, 2, SocketError: 10013,
                Route: new(DnsRouteStatus.OwnedTunnel)),
            new(ProbeTarget.Gstatic, DnsProbeOutcome.Rejected, 10, ResponseCode: 2,
                Route: new(DnsRouteStatus.OwnedTunnel)));
        var observed = new TunnelStatus(TunnelPhase.Disconnected, DateTimeOffset.UtcNow,
            UiFixtures.Primary.Name, "VPN_DNS_FAILED", DnsCheck: dns);
        model.SendOverride = (_, _) => Task.FromResult(new PipeReply(true, "OK", observed));
        try
        {
            model.Page = "connection";
            model.PollAsync().GetAwaiter().GetResult();
            model.PollAsync().GetAwaiter().GetResult();
            Assert.False(model.IsConnected); Assert.False(model.IsTunnelActive);
            Assert.Equal("VPN_DNS_FAILED", model.LastServiceCode);
            Assert.Contains("DNS attempt 1: Gstatic, Timeout, 3000 ms", model.SafeDiagnostics);
            Assert.Contains("DNS attempt 2: Slx, SocketError, 2 ms, socket=10013", model.SafeDiagnostics);
            Assert.Contains("DNS attempt 3: Gstatic, Rejected, 10 ms, rcode=2", model.SafeDiagnostics);
            Assert.Contains("route=OwnedTunnel", model.SafeDiagnostics);
            Assert.DoesNotContain("DNS attempt 4", model.SafeDiagnostics);
            Assert.DoesNotContain(UiFixtures.Primary.Host, model.SafeDiagnostics);
            Assert.DoesNotContain(UiFixtures.Primary.Name, model.SafeDiagnostics);
            Assert.DoesNotContain("synthetic-ui-fixture", model.SafeDiagnostics);
            Assert.DoesNotContain("1.1.1.1", model.SafeDiagnostics);
            foreach (var language in new[] { "ru", "en" })
            {
                model.Language = language;
                Sta.Resize(window, 940, 660);
                Assert.Equal(Text.Catalog["FaultVpnDns"], model.StatusDetail);
                Assert.Contains(Sta.Descendants<TextBlock>(window), block => block.Text == model.StatusDetail);
                Assert.Equal(Text.Catalog["Connect"], model.ConnectionAction);
                if (language == "ru") UiScreenshots.Save(window, "slx-dns-failure-ru-compact");
            }
            observed = observed with { DnsCheck = new(DateTimeOffset.UtcNow,
                new(ProbeTarget.Gstatic, DnsProbeOutcome.RouteUnavailable, 0,
                    Route: new(DnsRouteStatus.Unavailable, 1168))) };
            model.PollAsync().GetAwaiter().GetResult();
            Assert.Contains("route=Unavailable, route_error=1168", model.SafeDiagnostics);
            Assert.DoesNotContain("DNS attempt 2", model.SafeDiagnostics);
            observed = observed with { Code = "VPN_DNS_ROUTE_FAILED", DnsCheck = new(DateTimeOffset.UtcNow,
                new(ProbeTarget.Gstatic, DnsProbeOutcome.RouteMismatch, 2500,
                    Route: new(DnsRouteStatus.OtherInterface, PrefixLength: 32, RouteMetric: 10))) };
            foreach (var language in new[] { "ru", "en" })
            {
                model.Language = language;
                model.PollAsync().GetAwaiter().GetResult();
                model.PollAsync().GetAwaiter().GetResult();
                Sta.Resize(window, 940, 660);
                Assert.Equal("VPN_DNS_ROUTE_FAILED", model.LastServiceCode);
                Assert.Equal(Text.Catalog["FaultVpnDnsRoute"], model.StatusDetail);
                Assert.Contains("route=OtherInterface, prefix_length=32, route_metric=10", model.SafeDiagnostics);
                Assert.DoesNotContain("1.1.1.1", model.SafeDiagnostics);
                Assert.Contains(Sta.Descendants<TextBlock>(window), block => block.Text == model.StatusDetail);
                Assert.Equal(Text.Catalog["Connect"], model.ConnectionAction);
                Assert.False(model.IsConnected); Assert.False(model.IsTunnelActive);
                if (language == "ru") UiScreenshots.Save(window, "slx-dns-route-failure-ru-compact");
            }
            observed = new(TunnelPhase.Disconnected, DateTimeOffset.UtcNow);
            model.PollAsync().GetAwaiter().GetResult();
            Assert.DoesNotContain("DNS attempt", model.SafeDiagnostics);
            Assert.DoesNotContain("DNS check UTC", model.SafeDiagnostics);
        }
        finally
        {
            window.DataContext = previous;
            Text.Catalog.SetLanguage("ru");
            Sta.Resize(window, 1200, 820);
        }
    }
}
