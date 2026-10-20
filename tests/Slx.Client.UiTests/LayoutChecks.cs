using System.Windows;
using System.Windows.Controls;
using Slx.Client.Core;
using Slx.Client.Desktop;
using Slx.Client.Desktop.ViewModels;
using Xunit;
namespace Slx.Client.UiTests;
internal static class LayoutChecks
{
    public static void Run(MainWindow window, MainViewModel model)
    {
        Assert.Equal("olive", model.Theme);
        model.Page = "connection"; window.Width = 1200; window.Height = 820; Sta.Pump();
        AssertAboveFold(window);
        UiScreenshots.Save(window, "slx-populated-selected");
        var selected = Sta.Descendants<StackPanel>(window).Single(x => x.Name == "SelectedAppsSection");
        Assert.Equal(Visibility.Visible, selected.Visibility);
        model.IsWholeMode = true; Sta.Pump();
        Assert.Equal(Visibility.Collapsed, selected.Visibility);
        AssertAboveFold(window);
        var liveDividers = Sta.Descendants<Border>(window).Where(x => x.Height == 1 && x.IsVisible).ToList();
        Assert.Empty(liveDividers);
        UiScreenshots.Save(window, "slx-populated-whole");
        model.IsSelectedMode = true;
        var original = model.Applications.ToHashSet();
        UiFixtures.AllowSyntheticApplications(model);
        foreach (var name in new[] { "Design Studio", "Code Editor", "Music Player", "Video Player", "Research Browser", "Team Messenger" })
            model.AddDiscoveredCommand.Execute(new AppRule(Guid.NewGuid(), name, @"C:\SLX UI Fixture\" + name + ".exe"));
        window.Width = 940; window.Height = 660; Sta.Pump();
        var scroll = Sta.Descendants<ScrollViewer>(window).Single(x => x.Name == "ConnectionScroll");
        var bar=Sta.Descendants<System.Windows.Controls.Primitives.ScrollBar>(scroll).Single(x=>x.Orientation==Orientation.Vertical);
        var edge=bar.TransformToAncestor(window).TransformBounds(new Rect(bar.RenderSize));
        Assert.InRange(window.ActualWidth-edge.Right,0,3);Assert.InRange(bar.ActualWidth,10,12);
        scroll.ScrollToBottom();Sta.Pump();Assert.True(scroll.VerticalOffset>0);scroll.ScrollToTop();Sta.Pump();
        Assert.True(scroll.ViewportWidth > 0); Assert.True(scroll.ExtentWidth <= scroll.ViewportWidth + 1);
        UiScreenshots.Save(window, "slx-populated-narrow");
        foreach (var item in model.Applications.Where(item => !original.Contains(item)).ToArray()) model.RemoveAppCommand.Execute(item);
        model.IsWholeMode = true;
        model.Page = "applications"; Sta.Pump();
        Assert.All(Sta.Descendants<TextBlock>(window).Where(x => x.Text == UiFixtures.Preferences(Slx.Client.Core.RoutingMode.SelectedApplications).Applications![1].Name), x => Assert.Equal(TextTrimming.CharacterEllipsis, x.TextTrimming));
        UiScreenshots.Save(window, "slx-populated-apps-narrow");
        window.Width = 1200; window.Height = 820; model.Page = "settings"; model.Language = "en"; model.Theme = "terminal"; Sta.Pump();
        UiScreenshots.Save(window, "slx-populated-settings-en");
    }
    private static void AssertAboveFold(MainWindow window)
    {
        var scroll = Sta.Descendants<ScrollViewer>(window).Single(x => x.Name == "ConnectionScroll");
        var subscription = Sta.Descendants<Border>(window).Single(x => x.Name == "SubscriptionSection");
        var bounds = subscription.TransformToAncestor(scroll).TransformBounds(new Rect(subscription.RenderSize));
        Assert.True(bounds.Bottom <= scroll.ViewportHeight + 1, $"Subscription below fold: {bounds.Bottom:F0} > {scroll.ViewportHeight:F0}");
        var button = Sta.Descendants<Button>(window).Single(x => x.Name == "ConnectAction");
        Assert.True(button.TransformToAncestor(scroll).TransformBounds(new Rect(button.RenderSize)).Bottom < scroll.ViewportHeight);
    }
}
