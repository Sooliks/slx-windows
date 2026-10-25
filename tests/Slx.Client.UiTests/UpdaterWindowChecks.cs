using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Slx.Client.Updater;
using Slx.Client.Windows;
using Xunit;
namespace Slx.Client.UiTests;
internal static class UpdaterWindowChecks
{
    private static readonly IntegratedUpdatePhase[] Phases = [IntegratedUpdatePhase.WaitingForApplication,
        IntegratedUpdatePhase.AwaitingPermission, IntegratedUpdatePhase.Installing, IntegratedUpdatePhase.Failed, IntegratedUpdatePhase.RestartRequired];
    public static void Run()
    {
        foreach (var language in new[] { "ru", "en" })
        {
            // This overload creates no session and registers no Loaded/run/close-business handler.
            var window = new UpdateWindow("0.1.0-alpha.25", language)
            { ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual, Left = -20000, Top = -20000 };
            try
            {
                window.Show(); Sta.Pump();
                Assert.Equal(440, window.MinWidth);
                foreach (var width in new[] { 530, 440 })
                {
                    Sta.Resize(window, width, width == 440 ? 520 : 480);
                    foreach (var phase in Phases)
                    {
                        window.PresentPhase(phase); Sta.Pump(); AssertFits(window);
                        var terminal = phase is IntegratedUpdatePhase.Failed or IntegratedUpdatePhase.RestartRequired;
                        Assert.Equal(terminal ? Visibility.Collapsed : Visibility.Visible, window.Activity.Visibility);
                        Assert.Equal(terminal, window.ReturnButton.IsVisible);
                        Assert.Equal(phase == IntegratedUpdatePhase.Failed, window.RetryButton.IsVisible);
                        UiScreenshots.Save(window, $"slx-updater-{phase.ToString().ToLowerInvariant()}-{language}-{width}");
                    }
                }
                window.PresentPhase(IntegratedUpdatePhase.Failed); Sta.Resize(window, 530, 600);
                ((FrameworkElement)window.Content).LayoutTransform = new ScaleTransform(1.25, 1.25);
                Sta.Pump(); AssertFits(window); UiScreenshots.Save(window, $"slx-updater-text-scale125-{language}");
                ((FrameworkElement)window.Content).LayoutTransform = Transform.Identity;
                window.ApplyContrast(true); Sta.Resize(window, 530, 480); Sta.Pump(); AssertFits(window);
                Assert.Same(SystemColors.WindowBrush, window.Resources["UpdateCanvas"]);
                UiScreenshots.Save(window, $"slx-updater-highcontrast-{language}");
                window.ApplyContrast(false);
                // Fixture buttons cannot launch a session/application or shut down the test Application.
                window.RetryButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                window.ReturnButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Sta.Pump(); Assert.True(window.IsVisible);
                Assert.Equal(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting(window.Heading));
            }
            finally { window.Close(); Sta.Pump(); }
        }
    }
    private static void AssertFits(UpdateWindow window)
    {
        foreach (var block in Sta.Descendants<TextBlock>(window).Where(block => block.IsVisible))
        {
            var bounds = block.TransformToAncestor(window).TransformBounds(new Rect(block.RenderSize));
            Assert.True(bounds.Left >= 0 && bounds.Top >= 0 && bounds.Right <= window.ActualWidth + 1 && bounds.Bottom <= window.ActualHeight + 1,
                $"Updater text exceeds viewport: {block.Name}");
            Assert.DoesNotContain("╳", block.Text);
        }
        foreach (var button in Sta.Descendants<Button>(window).Where(button => button.IsVisible))
        {
            Assert.True(button.ActualHeight >= 44); Assert.True(button.Focusable); Assert.NotNull(button.FocusVisualStyle);
            Assert.NotNull(button.Template);
            var bounds = button.TransformToAncestor(window).TransformBounds(new Rect(button.RenderSize));
            Assert.True(bounds.Left >= 0 && bounds.Right <= window.ActualWidth && bounds.Bottom <= window.ActualHeight);
        }
        Assert.True(window.ReturnButton.Style is not null && window.RetryButton.Style is not null);
    }
}
