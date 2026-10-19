using System.Windows;
using System.Windows.Controls;
using Slx.Client.Core;
using Slx.Client.Desktop;
using Slx.Client.Desktop.Presentation;
using Slx.Client.Desktop.ViewModels;
using Xunit;
namespace Slx.Client.UiTests;
internal static class FollowupChecks
{
    public static void Run(MainWindow window)
    {
        foreach (var key in new object[] { typeof(Button), typeof(RadioButton), typeof(CheckBox), "TrayMenuItem" })
        {
            var style = (Style)window.FindResource(key);
            var template = style.Setters.OfType<Setter>().FirstOrDefault(setter => setter.Property == Control.TemplateProperty)?.Value as ControlTemplate;
            Assert.NotNull(template);
            foreach (var trigger in template.Triggers.OfType<Trigger>().Where(trigger =>
                trigger.Property.Name is "IsMouseOver" or "IsHighlighted" or "IsKeyboardFocused" or "IsKeyboardFocusWithin"))
                Assert.DoesNotContain(trigger.Setters.OfType<Setter>(), setter =>
                    setter.Property.Name is "Margin" or "Padding" or "BorderThickness" or "Width" or "Height" or "FontSize" or "FontWeight" or "LayoutTransform" or "RenderTransform");
        }
        Assert.Single(Sta.Descendants<AmbientBackdrop>(window));
        CheckQuickAdd();
        LibraryChecks.Run(window);
        LibraryLoadingChecks.Run();
        AutoRoutingChecks.Run();
        DashboardChecks.Run(window);
        CheckStartupRefresh();
    }
    private static void CheckQuickAdd()
    {
        using var model = new MainViewModel(false);
        var first = new AppRule(Guid.NewGuid(), "Messenger", @"C:\Synthetic\Messenger.exe");
        model.Discovered.Add(first);
        model.Discovered.Add(new(Guid.NewGuid(), "Browser", @"C:\Synthetic\Browser.exe"));
        model.DiscoverySearch = "mess";
        Assert.Equal(first, Assert.Single(model.FilteredDiscovered));
        model.AddDiscoveredCommand.Execute(first);
        Assert.Single(model.Applications);
        Assert.Empty(model.FilteredDiscovered);
        Assert.False(model.DiscoveryVisible);
        model.DiscoverySearch = "";
        Assert.Single(model.FilteredDiscovered);
        Assert.NotNull(model.AddExecutableCommand);
    }
    private static MainViewModel CachedModel() => new(false,
        UiFixtures.Subscription with { Snapshot = UiFixtures.Subscription.Snapshot with { Profiles = [UiFixtures.Alternative] } });
    private static void CheckStartupRefresh()
    {
        using (var model = CachedModel())
        {
            int calls = 0;
            var pending = new TaskCompletionSource<SubscriptionSnapshot>();
            model.StartupFetchOverride = (_, _) => { calls++; return pending.Task; };
            var refresh = model.RefreshOnStartupAsync();
            Assert.Single(model.Profiles);
            Assert.True(model.IsSubscriptionRefreshing);
            Assert.False(model.IsBusy);
            pending.SetResult(UiFixtures.Subscription.Snapshot); Sta.Pump();
            Assert.True(refresh.IsCompletedSuccessfully);
            Assert.Equal(2, model.Profiles.Count);
            Assert.Equal(UiFixtures.Alternative.Id, model.SelectedProfile?.Id);
            model.RefreshOnStartupAsync().GetAwaiter().GetResult();
            Assert.Equal(1, calls);
        }
        using (var model = CachedModel())
        {
            model.StartupFetchOverride = (_, _) => Task.FromException<SubscriptionSnapshot>(new ClientFault("NETWORK_UNAVAILABLE"));
            model.RefreshOnStartupAsync().GetAwaiter().GetResult();
            Assert.Single(model.Profiles);
            Assert.False(model.IsSubscriptionRefreshing);
        }
        using (var model = CachedModel())
        {
            var pending = new TaskCompletionSource<SubscriptionSnapshot>();
            model.StartupFetchOverride = (_, _) => pending.Task;
            var refresh = model.RefreshOnStartupAsync();
            model.OpenImportCommand.Execute(null);
            pending.SetResult(UiFixtures.Subscription.Snapshot); Sta.Pump();
            Assert.True(refresh.IsCompletedSuccessfully);
            Assert.Single(model.Profiles);
            Assert.True(model.ImportVisible);
        }
    }
}
