using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Slx.Client.Core;
using Slx.Client.Desktop;
using Slx.Client.Desktop.ViewModels;
using Xunit;
namespace Slx.Client.UiTests;
internal static class AdaptiveNavigationChecks
{
    public static void Run(MainWindow window, MainViewModel model)
    {
        model.Page = "connection"; model.Theme = "olive"; model.Language = "ru";
        var profile = model.SelectedProfile;
        var application = model.Applications[0];
        var sidebar = Sta.Descendants<Border>(window).Single(element => element.Name == "Sidebar");
        var navigation = Sta.Descendants<Button>(sidebar).Where(button => ReferenceEquals(button.Command, model.NavigateCommand)).ToArray();
        Assert.Equal(4, navigation.Length);
        Sta.Resize(window, 1200, 820);
        Assert.Equal(204, sidebar.ActualWidth);
        Assert.All(navigation, button => Assert.True(Assert.Single(Sta.Descendants<TextBlock>(button)).IsVisible));
        foreach (var button in Sta.Descendants<Button>(sidebar))
            Assert.All(Sta.Descendants<TextBlock>(button), text => AssertTextFits(text, button));
        CheckRoutingCaptions(window);
        UiScreenshots.Save(window, "slx-navigation-wide");
        Sta.Resize(window, 940, 660);
        Assert.Equal(76, sidebar.ActualWidth);
        Assert.All(navigation, button =>
        {
            Assert.False(Assert.Single(Sta.Descendants<TextBlock>(button)).IsVisible);
            Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(button)));
            Assert.NotNull(button.ToolTip);
            Assert.True(button.ActualWidth >= 44 && button.ActualHeight >= 44);
        });
        Assert.Same(profile, model.SelectedProfile);
        Assert.Same(application, model.Applications[0]);
        CheckRoutingCaptions(window);
        var connection = navigation.Single(button => Equals(button.CommandParameter, "connection"));
        var applications = navigation.Single(button => Equals(button.CommandParameter, "applications"));
        Assert.False(string.IsNullOrEmpty(AutomationProperties.GetItemStatus(connection)));
        model.NavigateCommand.Execute("applications"); Sta.Pump();
        Assert.Empty(AutomationProperties.GetItemStatus(connection));
        Assert.False(string.IsNullOrEmpty(AutomationProperties.GetItemStatus(applications)));
        model.NavigateCommand.Execute("connection"); Sta.Pump();
        UiScreenshots.Save(window, "slx-navigation-compact");
        model.SendOverride = (_, _) => Task.FromResult(new PipeReply(false, "TRAFFIC_PROTECTION_FAILED", new(TunnelPhase.Disconnected, DateTimeOffset.UtcNow)));
        model.PollAsync().GetAwaiter().GetResult(); Sta.Pump();
        var scroll = Sta.Descendants<ScrollViewer>(window).Single(element => element.Name == "ConnectionScroll");
        Assert.True(scroll.ExtentWidth <= scroll.ViewportWidth + 1);
        UiScreenshots.Save(window, "slx-connection-unknown-compact");
        Sta.Resize(window, 1200, 820);
        UiScreenshots.Save(window, "slx-connection-unknown-wide");
        model.SendOverride = null;
    }
    private static void CheckRoutingCaptions(MainWindow window)
    {
        foreach (var option in Sta.Descendants<RadioButton>(window).Where(option => option.GroupName == "ConnectionMode"))
            Assert.All(Sta.Descendants<TextBlock>(option), text => AssertTextFits(text, option));
    }
    private static void AssertTextFits(TextBlock text, FrameworkElement control)
    {
        var bounds = text.TransformToAncestor(control).TransformBounds(new Rect(text.RenderSize));
        Assert.True(bounds.Right <= control.ActualWidth + 1 && bounds.Bottom <= control.ActualHeight + 1,
            $"Caption clipped by {control.GetType().Name}: {text.Text}");
    }
}
