using System.Windows;
using System.Windows.Controls;
using Slx.Client.Core;
using Slx.Client.Desktop;
using Slx.Client.Desktop.Localization;
using Slx.Client.Desktop.Presentation;
using Slx.Client.Desktop.ViewModels;
using Xunit;
namespace Slx.Client.UiTests;
public sealed class PopulatedUiTests
{
    [Fact]
    public Task PopulatedSelectionTemplatesAndLayoutRemainUsable() => Sta.Run(() =>
    {
        using var model = new MainViewModel(false, UiFixtures.Subscription, UiFixtures.Preferences(RoutingMode.SelectedApplications));
        var window = new MainWindow(model, true) { ShowInTaskbar = false, Left = -20000, Top = -20000 };
        window.Show(); Sta.Pump();
        try
        {
            CaptionChecks.Run(window, model);
            ComboChecks.Run(window, model);
            LayoutChecks.Run(window, model);
            AdaptiveNavigationChecks.Run(window, model);
            Assert.NotEqual("ServiceHelp", ServiceStateCopy.DetailKey("IPC_INVALID"));
            Assert.Equal("ServiceRequestHelp", ServiceStateCopy.DetailKey("IPC_INVALID"));
            Assert.Equal("ServiceUnknownHelp", ServiceStateCopy.DetailKey("SERVICE_TIMEOUT"));
            Assert.Equal("FaultServiceSecurity", ServiceStateCopy.DetailKey("SERVICE_ACCESS_DENIED"));
            Assert.Equal("SERVICE_FAILURE", ServiceStateCopy.SafeCode("https://example.invalid/private"));
            CheckTrayMenu(window);
            CheckUnknownExit(window);
            FollowupChecks.Run(window);
            UpdateChecks.Run(window);
            ApplicationUpdateVisualChecks.Run(window);
            StartupAutoConnectChecks.Run();
            SettingsStartupChecks.Run(window);
            MotionChecks.Run(window, model);
            PresentationStateChecks.Run();
            LocalizationChecks.Run();
            ThemeContrastChecks.Run();
            RoutingDeferralChecks.Run();
            AppChoiceStateChecks.Run(window);
            UpdateReuseChecks.Run();
        }
        finally { window.Close(); Sta.Pump(); }
    });
    private static void CheckUnknownExit(Window window)
    {
        var dialog = new ExitUnknownDialog(window); dialog.Show(); Sta.Pump();
        var buttons = Sta.Descendants<Button>(dialog).ToList();
        var cancel = buttons.Single(x => x.IsCancel);
        Assert.True(cancel.IsDefault);
        Assert.Equal(Text.Catalog["Cancel"], cancel.Content);
        Assert.Contains(buttons, x => Equals(x.Content, Text.Catalog["ExitUiOnly"]) && !x.IsDefault);
        UiScreenshots.Save(dialog, "slx-exit-unknown"); dialog.Close(); Sta.Pump();
    }
    private static void CheckTrayMenu(Window window)
    {
        var menu = new ContextMenu { Style = (Style)window.FindResource("TrayMenu"), PlacementTarget = window, HorizontalOffset = -20000 };
        menu.Items.Add(new MenuItem { Header = Text.Catalog["TrayOpen"], Style = (Style)window.FindResource("TrayMenuItem") });
        menu.Items.Add(new Separator { Style = (Style)window.FindResource("TrayMenuSeparator") });
        menu.Items.Add(new MenuItem { Header = Text.Catalog["TrayExit"], Style = (Style)window.FindResource("TrayMenuItem") });
        menu.IsOpen = true; Sta.Pump();
        Assert.NotNull(menu.Template);
        Assert.Equal(2, Sta.Descendants<MenuItem>(menu).Count());
        UiScreenshots.Save(menu, "slx-tray-menu");
        menu.IsOpen = false; Sta.Pump();
    }
}
