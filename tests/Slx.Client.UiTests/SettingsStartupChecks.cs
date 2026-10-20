using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using Slx.Client.Core;
using Slx.Client.Desktop;
using Slx.Client.Desktop.Localization;
using Slx.Client.Desktop.Presentation;
using Slx.Client.Desktop.ViewModels;
using Xunit;
namespace Slx.Client.UiTests;
internal static class SettingsStartupChecks
{
    public static void Run(MainWindow window)
    {
        var previous = (MainViewModel)window.DataContext;
        using var enabled = new MainViewModel(false, UiFixtures.Subscription,
            UiFixtures.Preferences(RoutingMode.SelectedApplications) with { AutoStart = true, AutoConnectOnStartup = true });
        using var disabled = new MainViewModel(false, UiFixtures.Subscription,
            UiFixtures.Preferences(RoutingMode.SelectedApplications) with { AutoStart = false, AutoConnectOnStartup = true });
        try
        {
            enabled.Page = "settings"; window.DataContext = enabled; window.Width = 940; window.Height = 660; Sta.Pump();
            var option = Option(window); Assert.True(option.IsEnabled); Assert.True(option.IsChecked);
            var peer = new CheckBoxAutomationPeer(option);
            Assert.Equal(Text.Catalog["AutoConnectStartup"], peer.GetName());
            var toggle = Assert.IsAssignableFrom<IToggleProvider>(peer.GetPattern(PatternInterface.Toggle));
            toggle.Toggle(); Sta.Pump(); Assert.False(enabled.AutoConnectOnStartup); Assert.True(enabled.AutoStart);
            toggle.Toggle(); Sta.Pump(); Assert.True(enabled.AutoConnectOnStartup);
            AssertCaptionsFit(window, option);
            UiScreenshots.Save(window, "slx-settings-startup-enabled-narrow");
            enabled.Language = "en"; enabled.Theme = "terminal"; window.Width = 1200; window.Height = 820; Sta.Pump();
            AssertCaptionsFit(window, option);
            UiScreenshots.Save(window, "slx-settings-startup-enabled-wide-en");
            disabled.Page = "settings"; window.DataContext = disabled; Sta.Pump();
            Assert.False(Option(window).IsEnabled); Assert.True(Option(window).IsChecked); Assert.True(disabled.AutoConnectOnStartup);
        }
        finally
        {
            window.DataContext = previous; Text.Catalog.SetLanguage(previous.Language); ThemeManager.Apply(previous.Theme); Sta.Pump();
        }
    }
    private static CheckBox Option(MainWindow window) => Sta.Descendants<CheckBox>(window).Single(item => item.Name == "AutoConnectStartupOption");
    private static void AssertCaptionsFit(MainWindow window, CheckBox option)
    {
        foreach (var text in Sta.Descendants<TextBlock>(option))
        {
            var bounds = text.TransformToAncestor(option).TransformBounds(new Rect(text.RenderSize));
            Assert.True(bounds.Right <= option.ActualWidth + 1 && bounds.Bottom <= option.ActualHeight + 1);
        }
        var scroll = Sta.Descendants<ScrollViewer>(window).First();
        Assert.True(scroll.ExtentWidth <= scroll.ViewportWidth + 1);
    }
}
