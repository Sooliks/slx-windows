using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using Slx.Client.Core;
using Slx.Client.Desktop;
using Slx.Client.Desktop.Localization;
using Slx.Client.Desktop.Presentation;
using Slx.Client.Desktop.ViewModels;
using Slx.Client.Desktop.Views;
using Slx.Client.Windows;
using Xunit;
namespace Slx.Client.UiTests;
internal static class LibraryPresentationChecks
{
    public static void Run(MainWindow window)
    {
        var original = (MainViewModel)window.DataContext;
        var width = window.Width; var height = window.Height;
        try
        {
            CheckPresentation(window, "ru", 940, 660, "ru-narrow");
            CheckPresentation(window, "en", 1200, 820, "en-wide");
        }
        finally
        {
            Text.Catalog.SetLanguage(original.Language); ThemeManager.Apply(original.Theme);
            window.DataContext = original; Sta.Resize(window, width, height);
        }
    }
    private static void CheckPresentation(MainWindow window, string language, double width, double height, string suffix)
    {
        using var model = new MainViewModel(false, initialPreferences: new(Language: language)) { CatalogEntries = [], Page = "applications" };
        var cached = SavedApplications();
        var captured = new DateTimeOffset(2026, 10, 19, 12, 30, 0, TimeSpan.Zero);
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Action<DiscoveryBatch>? publish = null; int saves = 0;
        model.DiscoveryCacheLoadOverride = () => Task.FromResult<ApplicationDiscoverySnapshot?>(new(captured, cached));
        model.DiscoveryCacheSaveOverride = _ => { saves++; return Task.CompletedTask; };
        model.CatalogFetchOverride = _ => Task.FromResult<IReadOnlyList<CatalogApplication>>([]);
        model.DiscoveryScanOverride = (_, callback, token) => { publish = callback; return completion.Task.WaitAsync(token); };
        model.WarmDiscoveryCacheAsync().GetAwaiter().GetResult();
        window.DataContext = model; Sta.Resize(window, width, height);
        Assert.Equal(12, model.FilteredLibrary.Count); Assert.True(model.HasMoreLibraryResults);
        Assert.Equal(Text.Catalog["LibrarySaved"], model.LibraryStatusText);
        Assert.True(model.HasLibraryObservation); var observation = model.LibraryObservationText;
        model.DiscoverCommand.Execute(null); Sta.Pump();
        Assert.NotNull(publish); Assert.True(model.Discovering); Assert.False(model.IsBusy);
        var view = Sta.Descendants<QuickApplicationsView>(window).Single();
        var search = Sta.Descendants<TextBox>(view).Single(element => element.Name == "QuickSearch");
        var status = Sta.Descendants<TextBlock>(view).Single(element => element.Name == "LibraryStatus");
        Assert.Equal(Text.Catalog["LibraryRefreshing"], status.Text);
        Assert.Equal(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting(status));
        Assert.Equal(Text.Catalog["FindRunningApp"], new TextBoxAutomationPeer(search).GetName());
        Assert.All(LibraryButtons(view, model), button => Assert.True(button.IsEnabled));
        Assert.False(model.DiscoverCommand.CanExecute(null));
        AssertFits(window); UiScreenshots.Save(window, "slx-library-cached-refresh-" + suffix);
        model.ShowMoreApplicationsCommand.Execute(null); Sta.Pump();
        Assert.Equal(cached.Length, model.FilteredLibrary.Count); Assert.False(model.HasMoreLibraryResults);
        SetSearch(search, "Google");
        var chrome = Assert.Single(model.FilteredLibrary);
        Assert.Equal("Google Chrome", chrome.Name);
        var chromeButton = Assert.Single(LibraryButtons(view, model));
        FocusManager.SetFocusedElement(window, search); search.CaretIndex = 3;
        var keyboardFocused = ReferenceEquals(Keyboard.FocusedElement, search);
        publish!(new(DiscoverySource.Running, [cached.Single(app => app.Name == chrome.Name), App("Google Drive")], true));
        PumpUntil(() => model.FilteredLibrary.Count == 2);
        Assert.Same(chrome, model.FilteredLibrary.Single(entry => entry.Name == chrome.Name));
        Assert.Contains(LibraryButtons(view, model), button => ReferenceEquals(button, chromeButton));
        Assert.Equal("Google", model.DiscoverySearch); Assert.Equal("Google", search.Text); Assert.Equal(3, search.CaretIndex);
        Assert.Same(search, FocusManager.GetFocusedElement(window));
        if (keyboardFocused) Assert.Same(search, Keyboard.FocusedElement);
        var secondChromePath = new AppRule(Guid.NewGuid(), chrome.Name, @"C:\SLX UI Fixture\Google Chrome\app-2\Chrome.exe");
        publish(new(DiscoverySource.Installed, [secondChromePath], true));
        PumpUntil(() => chrome.Paths?.Count == 2);
        Assert.Same(chrome, model.FilteredLibrary.Single(entry => entry.Name == chrome.Name));
        Assert.Contains(LibraryButtons(view, model), button => ReferenceEquals(button, chromeButton));
        Assert.Contains(secondChromePath.ExecutablePath, chrome.Detail); Assert.Equal(chrome.Detail, chromeButton.ToolTip);
        Assert.Equal("Google", search.Text); Assert.Equal(3, search.CaretIndex);
        Assert.Same(search, FocusManager.GetFocusedElement(window));
        UiScreenshots.Save(window, "slx-library-cached-search-" + suffix);
        SetSearch(search, "");
        foreach (var source in Enum.GetValues<DiscoverySource>().Where(source => source is not (DiscoverySource.Running or DiscoverySource.Installed)))
            publish(new(source, [], source != DiscoverySource.Shortcuts));
        completion.SetResult(true); PumpUntil(() => !model.Discovering);
        Assert.Equal(Text.Catalog["LibraryRefreshPartial"], status.Text);
        Assert.Equal(observation, model.LibraryObservationText); Assert.Equal(0, saves);
        Assert.Equal(cached.Length + 2, model.Discovered.Count); Assert.Equal(12, model.FilteredLibrary.Count);
        Assert.True(model.HasMoreLibraryResults); Assert.True(model.DiscoverCommand.CanExecute(null));
        Assert.All(LibraryButtons(view, model), button => Assert.True(button.IsEnabled));
        AssertFits(window); UiScreenshots.Save(window, "slx-library-partial-" + suffix);
    }
    private static IEnumerable<Button> LibraryButtons(DependencyObject view, MainViewModel model) => Sta.Descendants<Button>(view)
        .Where(button => ReferenceEquals(button.Command, model.AddLibraryCommand));
    private static void SetSearch(TextBox search, string value)
    {
        search.SetCurrentValue(TextBox.TextProperty, value);
        search.GetBindingExpression(TextBox.TextProperty)!.UpdateSource(); Sta.Pump();
    }
    private static AppRule[] SavedApplications() => new[]
    {
        "Arc Browser", "Brave", "Discord", "Figma", "Firefox", "Google Chrome", "Microsoft Edge", "Notion", "OBS Studio",
        "Obsidian", "Opera", "Roblox Player", "Slack", "Spotify", "Steam", "Telegram", "Visual Studio Code", "Zoom"
    }.Select(App).ToArray();
    private static AppRule App(string name) => new(Guid.NewGuid(), name, @"C:\SLX UI Fixture\" + name + ".exe");
    private static void AssertFits(MainWindow window) => Assert.All(Sta.Descendants<ScrollViewer>(window),
        scroll => Assert.True(scroll.ExtentWidth <= scroll.ViewportWidth + 1));
    private static void PumpUntil(Func<bool> condition)
    {
        var elapsed = Stopwatch.StartNew();
        while (!condition() && elapsed.Elapsed < TimeSpan.FromSeconds(3)) { Sta.Pump(); Thread.Sleep(1); }
        Assert.True(condition());
    }
}
