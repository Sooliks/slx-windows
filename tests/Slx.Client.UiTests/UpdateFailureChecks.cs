using System.Windows;
using System.Windows.Controls;
using Slx.Client.Core;
using Slx.Client.Desktop;
using Slx.Client.Desktop.Localization;
using Slx.Client.Desktop.ViewModels;
using Slx.Client.Desktop.Views;
using Slx.Client.Windows;
using Xunit;
namespace Slx.Client.UiTests;
internal static class UpdateFailureChecks
{
    private static readonly UpdateOffer Offer = new("0.1.0-alpha.20", UpdateSourcePolicy.ReleasePage("0.1.0-alpha.20"),
        UpdateSourcePolicy.InstallerName("0.1.0-alpha.20"),
        new("https://github.com/Sooliks/slx-windows/releases/download/v0.1.0-alpha.20/SLX-VPN-0.1.0-alpha.20-win-x64-setup.exe"), 1, new string('a', 64));
    public static void Run(MainWindow window)
    {
        var original = window.DataContext;
        using var model = new MainViewModel(false, UiFixtures.Subscription, UiFixtures.Preferences(RoutingMode.SelectedApplications));
        window.DataContext = model; model.Page = "settings"; model.Language = "ru";
        Sta.Resize(window, 940, 660);
        var updates = model.Updates;
        int downloads = 0, launches = 0;
        updates.DownloadOverride = (offer, _, _) =>
        { downloads++; return Task.FromResult(new PreparedUpdate(offer, @"C:\SLX UI Fixture\NeverLaunched.exe")); };
        updates.LaunchOverride = (_, _) => { launches++; return Task.FromResult(UpdateLaunchResult.Started); };
        try
        {
            CheckFailure(window, updates, "UPDATE_TIMEOUT", "UpdateCheckTimeout");
            Assert.False(updates.HasOffer); Assert.False(updates.ApplyCommand.CanExecute(null));
            UiScreenshots.Save(window, "slx-update-timeout-ru-narrow");
            var russian = updates.Status; var notifications = 0;
            updates.PropertyChanged += (_, _) => notifications++;
            model.Language = "en"; Sta.Pump();
            Assert.True(notifications > 0); Assert.NotEqual(russian, updates.Status);
            AssertDisplayed(window, updates, "UpdateCheckTimeout");
            CheckFailure(window, updates, "UPDATE_INVALID", "UpdateCheckInvalid");
            CheckFailure(window, updates, "UPDATE_UNAVAILABLE", "UpdateCheckFailed");
            CheckFailure(window, updates, null, "UpdateCheckFailed");
            const string unknown = "https://example.invalid/private?token=synthetic-secret";
            CheckFailure(window, updates, unknown, "UpdateCheckFailed");
            Assert.DoesNotContain("synthetic-secret", updates.Status);
            updates.CheckOverride = (_, _) => Task.FromException<UpdateCheckResult>(new ClientFault("UPDATE_TIMEOUT"));
            updates.CheckAsync(true).GetAwaiter().GetResult(); Sta.Pump();
            AssertDisplayed(window, updates, "UpdateCheckTimeout");
            Assert.Equal(0, downloads); Assert.Equal(0, launches);
            updates.CheckOverride = (_, _) => Task.FromResult(new UpdateCheckResult(UpdateCheckState.Available, Offer));
            updates.CheckAsync(true).GetAwaiter().GetResult();
            CheckFailure(window, updates, "UPDATE_TIMEOUT", "UpdateCheckTimeout");
            Assert.True(updates.HasOffer); Assert.True(updates.ShowBanner);
            Assert.Equal(Text.Catalog["UpdateActionDownload"], updates.ActionText);
            Assert.True(updates.ApplyCommand.CanExecute(null));
            updates.ApplyCommand.ExecuteAsync(null).GetAwaiter().GetResult();
            Assert.Equal(1, downloads); Assert.Equal(0, launches);
            CheckFailure(window, updates, "UPDATE_INVALID", "UpdateCheckInvalid");
            Assert.True(updates.HasOffer); Assert.Equal(Text.Catalog["UpdateActionInstall"], updates.ActionText);
            Assert.Equal(1, downloads); Assert.Equal(0, launches);
            model.Language = "ru"; Sta.Pump(); AssertDisplayed(window, updates, "UpdateCheckInvalid");
            updates.CheckOverride = (_, _) => Task.FromResult(new UpdateCheckResult(UpdateCheckState.Current));
            updates.CheckAsync(true).GetAwaiter().GetResult(); Sta.Pump();
            Assert.False(updates.HasOffer); AssertDisplayed(window, updates, "UpdateCurrent");
        }
        finally { window.DataContext = original; Text.Catalog.SetLanguage("ru"); }
    }
    private static void CheckFailure(MainWindow window, UpdateViewModel updates, string? code, string key)
    {
        updates.CheckOverride = (_, _) => Task.FromResult(new UpdateCheckResult(UpdateCheckState.Unavailable, ErrorCode: code));
        updates.CheckAsync(true).GetAwaiter().GetResult(); Sta.Pump();
        Assert.False(updates.IsBusy); Assert.True(updates.CheckCommand.CanExecute(null));
        AssertDisplayed(window, updates, key);
    }
    private static void AssertDisplayed(MainWindow window, UpdateViewModel updates, string key)
    {
        var expected = Text.Catalog[key];
        Assert.NotEqual(key, expected); Assert.Equal(expected, updates.Status); Assert.Equal(expected, updates.BannerDescription);
        var settings = Sta.Descendants<UpdateSettings>(window).Single();
        Assert.Contains(Sta.Descendants<TextBlock>(settings), block => block.Text == expected);
    }
}
