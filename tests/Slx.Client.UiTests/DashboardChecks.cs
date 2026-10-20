using System.Windows.Controls;
using Slx.Client.Core;
using Slx.Client.Desktop;
using Slx.Client.Desktop.ViewModels;
using Slx.Client.Desktop.Views;
using Xunit;
namespace Slx.Client.UiTests;
internal static class DashboardChecks
{
    public static void Run(MainWindow window)
    {
        var model=(MainViewModel)window.DataContext;model.Page="connection";model.IsSelectedMode=true;Sta.Pump();
        var section=Sta.Descendants<StackPanel>(window).Single(x=>x.Name=="SelectedAppsSection");
        var switches=Sta.Descendants<CheckBox>(section).ToArray();
        Assert.Equal(model.Applications.Count,switches.Length);
        var first=switches[0];first.IsChecked=false;Sta.Pump();
        Assert.Contains(first,Sta.Descendants<CheckBox>(section));Assert.False(model.Applications[0].Enabled);
        first.IsChecked=true;
        ConnectionFactsChecks.Run(window,model);
        model.SendOverride=(request,_)=>Task.FromResult(new PipeReply(true,"OK",new(TunnelPhase.Connected,DateTimeOffset.UtcNow,
            ProfileName:UiFixtures.Primary.Name,ConnectedAt:DateTimeOffset.UtcNow.AddMinutes(-2),Mode:RoutingMode.SelectedApplications,ProbePassed:true)));
        model.PollAsync().GetAwaiter().GetResult();Sta.Pump();
        Assert.Equal("Hysteria2",model.ActiveProtocol);Assert.Matches(@"^00:02:\d{2}$",model.ConnectionDuration);
        var connection=Sta.Descendants<ConnectionView>(window).Single();
        Sta.Descendants<ScrollViewer>(connection).Single(x=>x.Name=="ConnectionScroll").ScrollToBottom();Sta.Pump();
        UiScreenshots.Save(window,"slx-dashboard-facts");
        model.Page="applications";Sta.Pump();
        var library=Sta.Descendants<QuickApplicationsView>(window).Single();
        var itemsHost=Sta.Descendants<WrapPanel>(library).Single(panel=>panel.IsItemsHost);
        System.Windows.DependencyObject? parent=itemsHost;
        while(parent is not null && parent is not ScrollViewer)parent=System.Windows.Media.VisualTreeHelper.GetParent(parent);
        Assert.NotNull(parent);
        Assert.DoesNotContain((ScrollViewer)parent,Sta.Descendants<ScrollViewer>(library));
        model.CatalogEntries=Slx.Client.Windows.CatalogClient.Bundled();model.DiscoverySearch="Discord";Sta.Pump();
        Assert.Contains(model.FilteredLibrary,x=>x.Name=="Discord"&&x.Rule==null);
        UiScreenshots.Save(window,"slx-catalog-library");
        model.SendOverride=null;
    }
}
