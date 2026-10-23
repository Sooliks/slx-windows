using System.Windows;
using System.Globalization;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shell;
using Path = System.Windows.Shapes.Path;
using Slx.Client.Desktop;
using Slx.Client.Desktop.Localization;
using Slx.Client.Desktop.Presentation;
using Slx.Client.Desktop.ViewModels;
using Xunit;
namespace Slx.Client.UiTests;
internal static class CaptionChecks
{
    public static void Run(MainWindow window, MainViewModel model)
    {
        var language = model.Language; var wasVisible = window.IsVisible;
        var controls = (StackPanel)window.FindName("CaptionControls");
        var minimize = (Button)window.FindName("MinimizeCaption");
        var maximize = (Button)window.FindName("MaximizeCaption");
        var close = (Button)window.FindName("CloseCaption");
        var buttons = new[] { minimize, maximize, close };
        try
        {
            model.Language = "ru"; Sta.Pump();
            CheckGeometry(buttons);
            Assert.Equal(Text.Catalog["WindowMaximize"], AutomationProperties.GetName(maximize));
            Assert.Equal(Text.Catalog["WindowMaximize"], maximize.ToolTip);
            var square = Glyph(maximize).Data.ToString(CultureInfo.InvariantCulture);
            UiScreenshots.Save(controls, "slx-caption-normal", localOrigin: true);
            CheckHoverFixture(window, controls, buttons, close);
            // Hide the isolated fixture before maximizing so no test window replaces the user's desktop.
            window.Hide(); Invoke(maximize); Sta.Pump();
            Assert.Equal(WindowState.Maximized, window.WindowState);
            Assert.Equal(Text.Catalog["WindowRestore"], AutomationProperties.GetName(maximize));
            Assert.Equal(Text.Catalog["WindowRestore"], maximize.ToolTip);
            Assert.NotEqual(square, Glyph(maximize).Data.ToString(CultureInfo.InvariantCulture));
            CheckGeometry(buttons);
            UiScreenshots.Save(controls, "slx-caption-maximized", localOrigin: true);
            model.Language = "en"; Sta.Pump();
            Assert.Equal(Text.Catalog["WindowRestore"], AutomationProperties.GetName(maximize));
            Invoke(maximize); Sta.Pump();
            Assert.Equal(WindowState.Normal, window.WindowState);
            Assert.Equal(Text.Catalog["WindowMaximize"], AutomationProperties.GetName(maximize));
            Assert.Equal(square, Glyph(maximize).Data.ToString(CultureInfo.InvariantCulture));
        }
        finally
        {
            window.WindowState = WindowState.Normal; model.Language = language;
            if (wasVisible) window.Show(); Sta.Pump();
        }
    }
    private static void Invoke(Button button)
    {
        var peer = new ButtonAutomationPeer(button);
        Assert.IsAssignableFrom<IInvokeProvider>(peer.GetPattern(PatternInterface.Invoke)).Invoke();
    }
    private static Path Glyph(Button button) => Sta.Descendants<Path>(button).Single();
    private static void CheckGeometry(IEnumerable<Button> buttons)
    {
        foreach (var button in buttons)
        {
            Assert.Equal(46, button.ActualWidth); Assert.Equal(42, button.ActualHeight);
            Assert.True(button.Focusable); Assert.True(button.IsTabStop); Assert.NotNull(button.FocusVisualStyle);
            Assert.True(WindowChrome.GetIsHitTestVisibleInChrome(button));
            var area = Assert.Single(Sta.Descendants<Canvas>(button));
            Assert.Equal(12, area.ActualWidth); Assert.Equal(12, area.ActualHeight);
            var bounds = Glyph(button).RenderedGeometry.Bounds;
            var center = Glyph(button).TransformToAncestor(button).Transform(new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2));
            Assert.InRange(Math.Abs(center.X - button.ActualWidth / 2), 0, 0.05);
            Assert.InRange(Math.Abs(center.Y - button.ActualHeight / 2), 0, 0.05);
        }
    }
    private static void CheckHoverFixture(MainWindow window, StackPanel controls, Button[] buttons, Button close)
    {
        Assert.Same(window.FindResource("DangerWash"), Chrome.GetHover(close));
        foreach (var button in buttons)
        {
            var hover = (Border)button.Template.FindName("CaptionHover", button);
            hover.SetCurrentValue(Motion.FadeToProperty, 1.0);
        }
        // Deterministic final-state preview of the production hover trigger; no mouse or installed window is driven.
        close.SetCurrentValue(Control.ForegroundProperty, window.FindResource("Danger"));
        Sta.Pump(); CheckGeometry(buttons); UiScreenshots.Save(controls, "slx-caption-hover-fixture", localOrigin: true);
        foreach (var button in buttons)
        {
            var press = (Border)button.Template.FindName("CaptionPress", button);
            press.SetCurrentValue(Motion.FadeToProperty, 1.0);
        }
        Sta.Pump(); CheckGeometry(buttons); UiScreenshots.Save(controls, "slx-caption-press-fixture", localOrigin: true);
        foreach (var button in buttons)
        {
            ((Border)button.Template.FindName("CaptionHover", button)).SetCurrentValue(Motion.FadeToProperty, 0.0);
            ((Border)button.Template.FindName("CaptionPress", button)).SetCurrentValue(Motion.FadeToProperty, 0.0);
        }
        close.ClearValue(Control.ForegroundProperty);
    }
}
