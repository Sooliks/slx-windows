using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Slx.Client.Core;
using Slx.Client.Desktop;
using Slx.Client.Desktop.Localization;
using Slx.Client.Desktop.ViewModels;
using Slx.Client.Desktop.Views;
using Slx.Client.Windows;
using Xunit;
namespace Slx.Client.UiTests;
internal static class IntegratedUpdateChecks
{
    internal static readonly UpdateOffer Offer = new("0.1.0-alpha.99", UpdateSourcePolicy.ReleasePage("0.1.0-alpha.99"),
        UpdateSourcePolicy.InstallerName("0.1.0-alpha.99"),
        new("https://github.com/Sooliks/slx-windows/releases/download/v0.1.0-alpha.99/SLX-VPN-0.1.0-alpha.99-win-x64-setup.exe"), 1, new string('a', 64));
    public static void Run(MainWindow window)
    {
        BackgroundDownloadDoesNotApply();
        AutomaticInstallationNeedsOptInAndConfirmedOff();
        PreparedArtifactSurvivesCheckFailure();
        ActiveConfirmationAndVisuals(window);
        IntegratedUpdateOutcomeChecks.Run();
    }
    private static void BackgroundDownloadDoesNotApply()
    {
        int applies = 0; var pending = new TaskCompletionSource<PreparedUpdate>();
        IProgress<int>? reporter = null;
        using var updates = Model(true, () => false, () => true, (_, _, _, _) => { applies++; return Task.FromResult(UpdateApplyResult.Failed); });
        updates.DownloadOverride = (_, progress, _) => { reporter = progress; return pending.Task; };
        var checking = updates.CheckAsync(false);
        Assert.True(updates.IsDownloading); Assert.Equal(0, applies);
        reporter!.Report(100); Sta.Pump(); Assert.Equal(100, updates.Progress); Assert.False(updates.InstallerOpened);
        updates.LaterCommand.Execute(null); Assert.False(updates.ShowBanner); Assert.False(checking.IsCompleted);
        pending.SetResult(new(Offer, @"C:\SLX UI Fixture\NeverLaunched.exe")); Sta.Pump();
        Assert.True(checking.IsCompletedSuccessfully); Assert.True(updates.HasOffer); Assert.False(updates.IsDownloading);
        Assert.False(updates.InstallerOpened); Assert.Equal(0, applies);
        Assert.Equal(Text.Catalog["UpdateReadyActive"], updates.BannerDescription);
    }
    private static void AutomaticInstallationNeedsOptInAndConfirmedOff()
    {
        bool off = true; int applies = 0;
        using var updates = Model(false, () => off, () => !off, (_, resume, _, _) =>
        { Assert.False(resume); applies++; return Task.FromResult(UpdateApplyResult.Cancelled); });
        Prepare(updates); Assert.False(updates.InstallAutomatically); Assert.False(updates.IsInstallCountdown);
        updates.InstallAutomatically = true; Assert.True(updates.IsInstallCountdown);
        updates.CountdownElapsedOverride = () => TimeSpan.FromSeconds(20);
        off = false; updates.RefreshInstallAvailability();
        Complete(updates.TickCountdownAsync()); Assert.False(updates.IsInstallCountdown); Assert.Equal(0, applies);
        off = true; updates.RefreshInstallAvailability(); Assert.True(updates.IsInstallCountdown);
        updates.LaterCommand.Execute(null); Assert.False(updates.IsInstallCountdown);
        Complete(updates.TickCountdownAsync()); Assert.Equal(0, applies);
        Complete(updates.CheckAsync(true)); Assert.True(updates.IsInstallCountdown);
        Complete(updates.TickCountdownAsync()); Assert.Equal(1, applies);
        updates.RefreshInstallAvailability(); Assert.False(updates.IsInstallCountdown); // A declined Windows prompt never loops.
        Assert.True(updates.ApplyCommand.CanExecute(null));
    }
    private static void PreparedArtifactSurvivesCheckFailure()
    {
        using var updates = Model(false, () => true, () => false, (_, _, _, _) => Task.FromResult(UpdateApplyResult.Cancelled));
        int downloads = 0;
        updates.DownloadOverride = (offer, _, _) => { downloads++; return Task.FromResult(new PreparedUpdate(offer, @"C:\SLX UI Fixture\NeverLaunched.exe")); };
        Prepare(updates);
        updates.CheckOverride = (_, _) => Task.FromResult(new UpdateCheckResult(UpdateCheckState.Unavailable, ErrorCode: "UPDATE_TIMEOUT"));
        Complete(updates.CheckAsync(false)); // Automatic checking is off, so there is no background request.
        Assert.False(updates.HasCheckWarning);
        Complete(updates.CheckAsync(true));
        Assert.Equal(Text.Catalog["UpdateReady"], updates.Status); Assert.True(updates.HasCheckWarning);
        Assert.Equal(Text.Catalog["UpdateCheckTimeout"], updates.CheckWarning); Assert.True(updates.ApplyCommand.CanExecute(null));
        Complete(updates.ApplyCommand.ExecuteAsync(null)); Assert.Equal(1, downloads);
    }
    private static void ActiveConfirmationAndVisuals(MainWindow window)
    {
        using var model = new MainViewModel(false, UiFixtures.Subscription, UiFixtures.Preferences(RoutingMode.SelectedApplications));
        var previous = window.DataContext; window.DataContext = model; model.Language = "ru"; Sta.Resize(window, 940, 660);
        int applies = 0;
        using var updates = Model(false, () => false, () => true, (_, resume, _, _) =>
        { Assert.True(resume); applies++; return Task.FromResult(UpdateApplyResult.Cancelled); });
        var banner = Sta.Descendants<UpdateBanner>(window).Single();
        banner.DataContext = updates;
        try
        {
            Prepare(updates); Sta.Pump();
            var connect = Sta.Descendants<Button>(window).Single(button => button.Name == "ConnectAction");
            var bounds = connect.TransformToAncestor(window).TransformBounds(new Rect(connect.RenderSize));
            System.Windows.Input.FocusManager.SetFocusedElement(window, connect);
            Complete(updates.ApplyCommand.ExecuteAsync(null)); Sta.Pump();
            Assert.True(updates.ConfirmationVisible); Assert.Equal(0, applies);
            Assert.Same(connect, System.Windows.Input.FocusManager.GetFocusedElement(window));
            Assert.Equal(bounds, connect.TransformToAncestor(window).TransformBounds(new Rect(connect.RenderSize)));
            Assert.Contains(Sta.Descendants<Button>(banner), button => button.Name == "UpdateConfirmActive" && button.IsVisible && button.IsEnabled);
            Assert.All(Sta.Descendants<Button>(banner).Where(button => button.IsVisible), button => Assert.True(button.ActualHeight >= 40));
            AssertFits(window, banner); UiScreenshots.Save(window, "slx-update-integrated-confirm-ru-narrow");
            updates.CancelConfirmationCommand.Execute(null); Assert.False(updates.ConfirmationVisible); Assert.Equal(0, applies);
            Complete(updates.ApplyCommand.ExecuteAsync(null)); Complete(updates.ConfirmUpdateCommand.ExecuteAsync(null));
            Assert.Equal(1, applies); Assert.False(updates.InstallerOpened); Assert.True(updates.ApplyCommand.CanExecute(null));
            model.Language = "en"; Complete(updates.ApplyCommand.ExecuteAsync(null)); Sta.Resize(window, 1200, 820);
            AssertFits(window, banner); UiScreenshots.Save(window, "slx-update-integrated-confirm-en-wide");
        }
        finally { banner.SetBinding(FrameworkElement.DataContextProperty, new Binding("Updates")); window.DataContext = previous; Text.Catalog.SetLanguage("ru"); }
    }
    internal static UpdateViewModel Model(bool automatic, Func<bool> off, Func<bool> active,
        Func<PreparedUpdate, bool, IProgress<UpdateApplyProgress>, CancellationToken, Task<UpdateApplyResult>> apply)
    {
        var updates = new UpdateViewModel("0.1.0-alpha.24", automatic, null, (_, _) => { }, off,
            _ => throw new InvalidOperationException("Integrated flow must own its admission"), integratedApply: apply, connectionActive: active);
        updates.CheckOverride = (_, _) => Task.FromResult(new UpdateCheckResult(UpdateCheckState.Available, Offer));
        updates.DownloadOverride = (offer, _, _) => Task.FromResult(new PreparedUpdate(offer, @"C:\SLX UI Fixture\NeverLaunched.exe"));
        return updates;
    }
    internal static void Prepare(UpdateViewModel updates)
    { Complete(updates.CheckAsync(true)); Complete(updates.ApplyCommand.ExecuteAsync(null)); Assert.Equal(Text.Catalog["UpdateReady"], updates.Status); }
    internal static void Complete(Task task)
    { AutoRoutingChecks.Settle(() => task.IsCompleted); task.GetAwaiter().GetResult(); }
    private static void AssertFits(Window window, FrameworkElement element)
    {
        var bounds = element.TransformToAncestor(window).TransformBounds(new Rect(element.RenderSize));
        Assert.True(bounds.Left >= 0 && bounds.Top >= 0 && bounds.Right <= window.ActualWidth && bounds.Bottom <= window.ActualHeight);
        Assert.All(Sta.Descendants<TextBlock>(element).Where(block => block.IsVisible), block =>
            Assert.True(block.TransformToAncestor(window).TransformBounds(new Rect(block.RenderSize)).Right <= window.ActualWidth));
    }
}
