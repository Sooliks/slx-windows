using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using Slx.Client.Core;
using Slx.Client.Desktop;
using Slx.Client.Desktop.Localization;
using Slx.Client.Desktop.ViewModels;
using Slx.Client.Desktop.Views;
using Slx.Client.Windows;
using Xunit;
namespace Slx.Client.UiTests;
internal static class UpdatePresentationChecks
{
    private static readonly UpdateOffer Offer = new("0.1.0-alpha.18", UpdateSourcePolicy.ReleasePage("0.1.0-alpha.18"),
        UpdateSourcePolicy.InstallerName("0.1.0-alpha.18"), new("https://github.com/Sooliks/slx-windows/releases/download/v0.1.0-alpha.18/SLX-VPN-0.1.0-alpha.18-win-x64-setup.exe"), 1, new string('a', 64));
    public static void Run(MainWindow window)
    {
        var original = window.DataContext;
        using var model = new MainViewModel(false, UiFixtures.Subscription, UiFixtures.Preferences(RoutingMode.SelectedApplications));
        window.DataContext = model; model.Language = "ru"; Sta.Resize(window, 940, 660);
        var updates = model.Updates;
        try
        {
            SetService(model, TunnelPhase.Disconnected);
            var connect = Sta.Descendants<Button>(window).Single(button => button.Name == "ConnectAction");
            var subscription = Sta.Descendants<Border>(window).Single(border => border.Name == "SubscriptionSection");
            var originalConnect = Bounds(connect, window); var originalSubscription = Bounds(subscription, window);
            FocusManager.SetFocusedElement(window, connect);
            updates.CheckOverride = (_, _) => Task.FromResult(new UpdateCheckResult(UpdateCheckState.Available, Offer));
            updates.CheckAsync(true).GetAwaiter().GetResult(); Sta.Pump();
            Assert.Equal(originalConnect, Bounds(connect, window)); Assert.Equal(originalSubscription, Bounds(subscription, window));
            Assert.Same(connect, FocusManager.GetFocusedElement(window));
            var banner = Sta.Descendants<UpdateBanner>(window).Single();
            var apply = Sta.Descendants<Button>(banner).Single(button => button.Name == "UpdateApply");
            var later = Sta.Descendants<Button>(banner).Single(button => button.Name == "UpdateLater");
            Assert.Contains(Offer.Version, new ButtonAutomationPeer(apply).GetName());
            Assert.Equal(Text.Catalog["UpdateLater"], new ButtonAutomationPeer(later).GetName());
            Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetHelpText(later)));
            Assert.True(apply.Focusable); Assert.True(later.Focusable); Assert.NotNull(apply.FocusVisualStyle);
            Assert.All(Sta.Descendants<Button>(banner), button => Assert.True(button.ActualHeight >= 40));
            AssertFits(window, banner); UiScreenshots.Save(window, "slx-update15-available-ru-narrow");
            model.ImportVisible = true; Sta.Pump();
            Assert.False(banner.IsVisible);
            Assert.False(Sta.Descendants<Grid>(window).Single(grid => grid.Name == "UpdateOverlayHost").IsVisible);
            model.ImportVisible = false; Sta.Pump(); Assert.True(banner.IsVisible);
            var pending = new TaskCompletionSource<PreparedUpdate>(); IProgress<int>? reporter = null;
            CancellationToken downloadCancellation = default;
            updates.DownloadOverride = (_, progress, token) => { reporter = progress; downloadCancellation = token; return pending.Task; };
            var download = updates.ApplyCommand.ExecuteAsync(null); Sta.Pump();
            Assert.True(updates.IsDownloading); Assert.Equal(0, updates.Progress); Assert.NotNull(reporter);
            FocusManager.SetFocusedElement(window, connect);
            reporter!.Report(37); Sta.Pump();
            Assert.Equal(37, updates.Progress); Assert.Contains("37", updates.ProgressText);
            Assert.Same(connect, FocusManager.GetFocusedElement(window)); Assert.True(connect.IsEnabled);
            AssertFits(window, banner); UiScreenshots.Save(window, "slx-update15-downloading-ru-narrow");
            Assert.True(later.IsEnabled); updates.LaterCommand.Execute(null); Sta.Pump();
            Assert.False(banner.IsVisible); Assert.False(pending.Task.IsCompleted); Assert.False(downloadCancellation.IsCancellationRequested);
            Assert.Equal(originalConnect, Bounds(connect, window)); Assert.Equal(originalSubscription, Bounds(subscription, window));
            reporter.Report(48); Sta.Pump(); Assert.Equal(48, updates.Progress);
            model.Page = "settings"; Sta.Pump();
            var downloadSettings = Sta.Descendants<UpdateSettings>(window).Single(); downloadSettings.BringIntoView(); Sta.Pump();
            Assert.Contains(Sta.Descendants<TextBlock>(downloadSettings), block => block.IsVisible && block.Text == updates.ProgressText);
            UiScreenshots.Save(window, "slx-update15-deferred-download-settings-ru-narrow");
            pending.SetResult(new(Offer, @"C:\SLX UI Fixture\NeverLaunched.exe")); Sta.Pump();
            Assert.True(download.IsCompletedSuccessfully); Assert.False(updates.IsDownloading);
            Assert.Equal(Text.Catalog["UpdateReady"], updates.Status);
            Assert.False(banner.IsVisible);
            Assert.True(Sta.Descendants<Button>(downloadSettings).Single(button => button.Name == "UpdateSettingsApply").IsEnabled);
            reporter.Report(99); Sta.Pump(); Assert.Equal(48, updates.Progress);
            updates.CheckAsync(true).GetAwaiter().GetResult(); Assert.Equal(Text.Catalog["UpdateReady"], updates.Status);
            model.Page = "connection"; Sta.Pump();
            updates.CheckOverride = (_, _) => Task.FromResult(new UpdateCheckResult(UpdateCheckState.Unavailable));
            updates.CheckAsync(true).GetAwaiter().GetResult();
            SetService(model, TunnelPhase.Connected);
            Assert.Equal(Text.Catalog["UpdateCheckFailed"], updates.BannerDescription);
            SetService(model, TunnelPhase.Disconnected);
            Assert.Equal(Text.Catalog["UpdateCheckFailed"], updates.BannerDescription);
            updates.CheckOverride = (_, _) => Task.FromResult(new UpdateCheckResult(UpdateCheckState.Available, Offer));
            updates.CheckAsync(true).GetAwaiter().GetResult();
            SetService(model, TunnelPhase.Connected);
            Assert.False(updates.ApplyCommand.CanExecute(null)); Assert.True(updates.IsInstallBlocked);
            Assert.Equal(Text.Catalog["UpdateDisconnectFirst"], updates.BannerDescription);
            AssertFits(window, banner); UiScreenshots.Save(window, "slx-update15-vpn-blocked-ru-narrow");
            model.SendOverride = (_, _) => Task.FromResult(new PipeReply(false, "SERVICE_TIMEOUT", new(TunnelPhase.Disconnected, DateTimeOffset.UtcNow)));
            model.PollAsync().GetAwaiter().GetResult(); Sta.Pump();
            Assert.Equal(Text.Catalog["UpdateServiceUnconfirmed"], updates.InstallHint);
            Assert.Equal(updates.InstallHint, updates.BannerDescription);
            Assert.Equal(updates.InstallHint, AutomationProperties.GetHelpText(apply));
            AssertFits(window, banner); UiScreenshots.Save(window, "slx-update15-service-wait-ru-narrow");
            SetService(model, TunnelPhase.Disconnected);
            updates.LaunchOverride = (_, _) => Task.FromResult(UpdateLaunchResult.Cancelled);
            updates.ApplyCommand.ExecuteAsync(null).GetAwaiter().GetResult(); Sta.Pump();
            Assert.Equal(Text.Catalog["UpdateInstallCancelled"], updates.Status); Assert.True(apply.IsEnabled);
            AssertFits(window, banner); UiScreenshots.Save(window, "slx-update15-uac-cancel-ru-narrow");
            SetService(model, TunnelPhase.Connected);
            Assert.Equal(Text.Catalog["UpdateInstallCancelled"], updates.BannerDescription);
            Assert.Equal(Text.Catalog["UpdateDisconnectFirst"], updates.InstallHint);
            SetService(model, TunnelPhase.Disconnected);
            Assert.Equal(Text.Catalog["UpdateInstallCancelled"], updates.BannerDescription);
            var launches = 0;
            updates.LaunchOverride = (_, _) => { launches++; return Task.FromResult(UpdateLaunchResult.Started); };
            model.SendOverride = (_, _) => Task.FromResult(new PipeReply(false, "SERVICE_TIMEOUT", new(TunnelPhase.Disconnected, DateTimeOffset.UtcNow)));
            Assert.True(updates.ApplyCommand.CanExecute(null));
            updates.ApplyCommand.ExecuteAsync(null).GetAwaiter().GetResult(); Sta.Pump();
            Assert.Equal(0, launches); Assert.False(updates.ApplyCommand.CanExecute(null));
            Assert.Equal(Text.Catalog["UpdateServiceUnconfirmed"], updates.BannerDescription);
            Assert.Equal(updates.BannerDescription, updates.InstallHint); Assert.False(updates.ShowInstallHint);
            SetService(model, TunnelPhase.Disconnected);
            Assert.Equal(Text.Catalog["UpdateReady"], updates.BannerDescription);
            updates.LaunchOverride = (_, _) => Task.FromException<UpdateLaunchResult>(new ClientFault("UPDATE_INSTALL_FAILED"));
            updates.ApplyCommand.ExecuteAsync(null).GetAwaiter().GetResult();
            var next = new TaskCompletionSource<PreparedUpdate>(); IProgress<int>? nextReporter = null;
            updates.DownloadOverride = (_, progress, _) => { nextReporter = progress; return next.Task; };
            var secondDownload = updates.ApplyCommand.ExecuteAsync(null); Sta.Pump();
            Assert.Equal(0, updates.Progress); reporter.Report(88); Sta.Pump(); Assert.Equal(0, updates.Progress);
            nextReporter!.Report(64); Sta.Pump(); Assert.Equal(64, updates.Progress);
            next.SetException(new ClientFault("UPDATE_INTEGRITY_FAILED")); Sta.Pump(); Assert.True(secondDownload.IsCompletedSuccessfully);
            Assert.Equal(Text.Catalog["UpdateIntegrityFailed"], updates.Status);
            AssertFits(window, banner); UiScreenshots.Save(window, "slx-update15-integrity-failure-ru-narrow");
            updates.LaterCommand.Execute(null); Sta.Pump(); Assert.False(banner.IsVisible); Assert.True(updates.IsOfferDeferred);
            model.Page = "settings"; Sta.Pump();
            var settings = Sta.Descendants<UpdateSettings>(window).Single();
            var settingsApply = Sta.Descendants<Button>(settings).Single(button => button.Name == "UpdateSettingsApply");
            Assert.True(settingsApply.IsEnabled); Assert.Contains(Offer.Version, new ButtonAutomationPeer(settingsApply).GetName());
            settings.BringIntoView(); Sta.Pump(); UiScreenshots.Save(window, "slx-update15-deferred-settings-ru-narrow");
            updates.CheckAsync(true).GetAwaiter().GetResult(); model.Page = "connection"; model.Language = "en";
            Sta.Resize(window, 1200, 820);
            AssertFits(window, banner); UiScreenshots.Save(window, "slx-update15-available-en-wide");
        }
        finally { window.DataContext = original; Text.Catalog.SetLanguage("ru"); }
    }
    private static void SetService(MainViewModel model, TunnelPhase phase)
    {
        model.SendOverride = (_, _) => Task.FromResult(new PipeReply(true, "OK", new(phase, DateTimeOffset.UtcNow, ProbePassed: phase == TunnelPhase.Connected, ProtectionActive: phase == TunnelPhase.Connected)));
        model.PollAsync().GetAwaiter().GetResult(); Sta.Pump();
    }
    private static void AssertFits(Window window, UpdateBanner banner)
    {
        Assert.True(banner.ActualHeight < 320); Assert.True(banner.ActualWidth <= 480);
        var sidebar = Sta.Descendants<Border>(window).Single(border => border.Name == "Sidebar");
        var cardBounds = Bounds(banner, window);
        Assert.True(cardBounds.Left >= Bounds(sidebar, window).Right);
        Assert.True(cardBounds.Top >= 40 && cardBounds.Right <= window.ActualWidth && cardBounds.Bottom <= window.ActualHeight);
        var connect = Sta.Descendants<Button>(window).Single(button => button.Name == "ConnectAction");
        Assert.False(cardBounds.IntersectsWith(Bounds(connect, window)));
        foreach (var button in Sta.Descendants<Button>(banner))
        {
            var bounds = button.TransformToAncestor(banner).TransformBounds(new Rect(button.RenderSize));
            Assert.True(bounds.Right <= banner.ActualWidth + 1);
        }
        foreach (var scroll in Sta.Descendants<ScrollViewer>(window)) Assert.True(scroll.ExtentWidth <= scroll.ViewportWidth + 1);
        var status = Sta.Descendants<TextBlock>(banner).Single(block => block.Name == "UpdateBannerStatus");
        Assert.Equal(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting(status));
    }
    private static Rect Bounds(FrameworkElement element, Window window) =>
        element.TransformToAncestor(window).TransformBounds(new Rect(element.RenderSize));
}
