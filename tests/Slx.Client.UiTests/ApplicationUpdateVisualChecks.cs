using System.Windows;
using System.Windows.Controls;
using Slx.Client.Core;
using Slx.Client.Desktop;
using Slx.Client.Desktop.Localization;
using Slx.Client.Desktop.ViewModels;
using Xunit;
namespace Slx.Client.UiTests;
internal static class ApplicationUpdateVisualChecks
{
    public static void Run(MainWindow window)
    {
        var previous = window.DataContext;
        try
        {
            foreach (var language in new[] { "ru", "en" })
            {
                using var model = new MainViewModel(false, UiFixtures.Subscription, UiFixtures.Preferences(RoutingMode.SelectedApplications));
                model.Language = language;
                var observed = new TunnelStatus(TunnelPhase.Connected, DateTimeOffset.UtcNow, UiFixtures.Primary.Name,
                    ConnectedAt: DateTimeOffset.UtcNow.AddMinutes(-10), Mode: RoutingMode.SelectedApplications,
                    ProbePassed: true, ProtectionActive: true);
                PipeRequest? mutation = null;
                model.SendOverride = (request, _) =>
                {
                    if (request.Action == "status") return Task.FromResult(new PipeReply(true, "OK", observed));
                    mutation = request;
                    return Task.FromResult(new PipeReply(false, "ROUTING_UPDATE_UNCONFIRMED", observed));
                };
                window.DataContext = model; Sta.Resize(window, language == "ru" ? 940 : 1200, language == "ru" ? 660 : 820);
                model.PollAsync().GetAwaiter().GetResult(); Sta.Pump();
                var hint = Sta.Descendants<TextBlock>(window).Single(item => item.Name == "RulesLiveHint");
                Assert.True(hint.IsVisible); Assert.Equal(Text.Catalog["RulesNotice"], hint.Text);
                Assert.InRange(hint.ActualWidth, 1, window.ActualWidth);
                UiScreenshots.Save(window, "slx-rules14-live-" + language);
                model.Applications[0].Enabled = false;
                AutoRoutingChecks.Settle(() => mutation is not null && !model.IsBusy);
                Assert.True(model.IsConnected); Assert.True(model.RulesPending);
                Assert.Equal("update-applications", mutation!.Action);
                Assert.Equal(Text.Catalog["RulesApplyUnconfirmed"], model.Message);
                UiScreenshots.Save(window, "slx-rules14-unconfirmed-" + language);
                observed = observed with { ApplicationRevision = XrayRouteRevision.For(mutation.Plan!) };
                model.PollAsync().GetAwaiter().GetResult(); Sta.Pump();
                Assert.True(model.IsConnected); Assert.False(model.RulesPending); Assert.False(model.ShowMessage);
            }
        }
        finally { window.DataContext = previous; Text.Catalog.SetLanguage("ru"); Sta.Pump(); }
    }
}
