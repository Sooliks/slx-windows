using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Slx.Client.Core;
using Slx.Client.Desktop;
using Slx.Client.Desktop.Localization;
using Slx.Client.Desktop.ViewModels;
using Slx.Client.Windows;
using Xunit;
namespace Slx.Client.UiTests;
/// <summary>First-run setup: found Discord/Telegram/browsers are one-step additions; nothing is routed or imported implicitly.</summary>
internal static class QuickStartChecks
{
    private const string SyntheticLink = "https://slxvpn.com/sub/0123456789abcdef01234567/synthetic-ui-fixture-not-a-real-token";
    public static void Run()
    {
        Suggestions();
        Clipboard();
    }
    private static void Suggestions()
    {
        using var model = new MainViewModel(false, UiFixtures.Subscription, new ClientPreferences(Applications: []));
        UiFixtures.AllowSyntheticApplications(model);
        int commands = 0;
        model.SendOverride = (_, _) => { commands++; throw new InvalidOperationException("Quick start must not send service commands while VPN is off"); };
        Assert.True(model.ShowQuickStart); Assert.False(model.HasStarterApplications);
        Assert.Equal(Text.Catalog["QuickStartEmpty"], model.QuickStartBody);
        var found = new[] { App("Text editor", "editor.exe"), App("Counter-Strike 2", "cs2.exe"), App("Steam", "steam.exe"),
            App("Google Chrome", "chrome.exe"), App("Telegram", "Telegram.exe"), App("Discord", "Discord.exe") };
        model.DiscoveryCacheLoadOverride = () => Task.FromResult<ApplicationDiscoverySnapshot?>(new(DateTimeOffset.UtcNow.AddHours(-1), found));
        model.WarmDiscoveryCacheAsync().GetAwaiter().GetResult();
        Assert.Equal(new[] { "Discord", "Telegram", "Google Chrome" }, model.StarterApplications.Select(entry => entry.Name));
        Assert.Equal(Text.Catalog["QuickStartBody"], model.QuickStartBody);
        Assert.Contains("Counter-Strike 2", model.QuickStartDirect); Assert.Contains("Steam", model.QuickStartDirect);
        Assert.DoesNotContain("editor", model.QuickStartDirect, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(model.Applications); Assert.False(model.RulesPending);
        var window = new MainWindow(model, true)
        { ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual, Left = -20000, Top = -20000 };
        window.Show(); Sta.Resize(window, 1200, 820);
        try
        {
            var card = Sta.Descendants<Border>(window).Single(x => x.Name == "QuickStartCard");
            Assert.True(card.IsVisible);
            var buttons = Sta.Descendants<Button>(card).ToList();
            Assert.Equal(3, buttons.Count);
            Assert.All(buttons, button => Assert.True(button.ActualHeight >= 44 && button.IsEnabled));
            UiScreenshots.Save(window, "slx-quick-start");
            model.AddLibraryCommand.Execute(model.StarterApplications[0]);
            Until(() => model.Applications.Count == 1);
            var discord = Assert.Single(model.Applications);
            Assert.Equal("Discord", discord.Name); Assert.True(discord.Enabled);
            // The same first session keeps the remaining suggestions; the added program leaves the list.
            Assert.True(model.ShowQuickStart);
            Assert.Equal(new[] { "Telegram", "Google Chrome" }, model.StarterApplications.Select(entry => entry.Name));
            Sta.Pump(); Assert.True(card.IsVisible);
            model.IsWholeMode = true; Sta.Pump();
            Assert.False(model.ShowQuickStart); Assert.False(card.IsVisible);
            model.IsSelectedMode = true; Assert.True(model.ShowQuickStart);
        }
        finally { window.Close(); Sta.Pump(); }
        Assert.Equal(0, commands);
        using var returning = new MainViewModel(false, UiFixtures.Subscription, UiFixtures.Preferences(RoutingMode.SelectedApplications));
        returning.DiscoveryCacheLoadOverride = model.DiscoveryCacheLoadOverride;
        returning.WarmDiscoveryCacheAsync().GetAwaiter().GetResult();
        Assert.True(returning.HasStarterApplications); Assert.False(returning.ShowQuickStart);
        using var unsubscribed = new MainViewModel(false, null, new ClientPreferences(Applications: []));
        Assert.False(unsubscribed.ShowQuickStart);
    }
    private static void Clipboard()
    {
        using var model = new MainViewModel(false, null, new ClientPreferences(Applications: []));
        int reads = 0; string? clipboard = "  " + SyntheticLink + "\r\n";
        model.ClipboardTextOverride = () => { reads++; return clipboard; };
        Assert.Equal(0, reads); // Never read before the explicit import action.
        model.OpenImportCommand.Execute(null);
        Assert.Equal(1, reads); Assert.True(model.ImportVisible);
        Assert.Equal(SyntheticLink, model.ImportLink);
        Assert.Equal(Text.Catalog["ImportClipboardHint"], model.ImportFootnoteText);
        Assert.False(model.HasSubscription); // Offering a link is not an import.
        model.ImportLink = SyntheticLink + "x";
        Assert.Equal(Text.Catalog["ImportFootnote"], model.ImportFootnoteText);
        model.CancelOverlayCommand.Execute(null);
        foreach (var other in new[] { null, "", "discord.gg/invite", "https://example.invalid/sub/0123456789abcdef01234567/synthetic-ui-fixture-not-a-real-token",
            "http://slxvpn.com/sub/0123456789abcdef01234567/synthetic-ui-fixture-not-a-real-token", SyntheticLink + "?x=1", new string('a', 4096) })
        {
            clipboard = other;
            model.OpenImportCommand.Execute(null);
            Assert.Equal("", model.ImportLink); Assert.False(model.ShowMessage);
            Assert.Equal(Text.Catalog["ImportFootnote"], model.ImportFootnoteText);
            model.PasteLinkCommand.Execute(null);
            Assert.Equal("", model.ImportLink); Assert.Equal(Text.Catalog["ClipboardNoLink"], model.Message);
            model.CancelOverlayCommand.Execute(null);
        }
        clipboard = SyntheticLink;
        model.OpenImportCommand.Execute(null); model.ImportLink = "";
        model.PasteLinkCommand.Execute(null);
        Assert.Equal(SyntheticLink, model.ImportLink); Assert.False(model.ShowMessage);
        using var isolated = new MainViewModel(false, null, new ClientPreferences(Applications: []));
        isolated.OpenImportCommand.Execute(null); // Fixtures without an override never touch the real clipboard.
        Assert.Equal("", isolated.ImportLink);
    }
    private static AppRule App(string name, string executable) => new(Guid.NewGuid(), name, $@"C:\SLX UI Fixture\{name}\{executable}");
    private static void Until(Func<bool> complete)
    {
        var watch = Stopwatch.StartNew(); while (!complete() && watch.Elapsed < TimeSpan.FromSeconds(3)) Sta.Pump();
        Assert.True(complete());
    }
}
