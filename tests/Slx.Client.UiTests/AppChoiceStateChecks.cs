using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Slx.Client.Core;
using Slx.Client.Desktop;
using Slx.Client.Desktop.Localization;
using Slx.Client.Desktop.Presentation;
using Slx.Client.Desktop.ViewModels;
using Xunit;
namespace Slx.Client.UiTests;
internal static class AppChoiceStateChecks
{
    public static void Run(MainWindow window)
    {
        var previous=window.DataContext;var languageBefore=Text.Catalog.Language;
        try
        {
            foreach(var language in new[]{"ru","en"})
            {
                var preferences=UiFixtures.Preferences(RoutingMode.SelectedApplications);
                preferences.Applications![1]=preferences.Applications![1] with{Enabled=false};
                using var model=new MainViewModel(false,UiFixtures.Subscription,preferences);
                model.Language=language;window.DataContext=model;
                Sta.Resize(window,language=="ru"?940:1200,language=="ru"?660:820);
                int mutations=0;bool unavailable=false;
                var observed=new TunnelStatus(TunnelPhase.Disconnected,DateTimeOffset.UtcNow);
                model.SendOverride=(request,_)=>
                {
                    if(request.Action!="status")
                    {
                        mutations++;Assert.Equal("update-applications",request.Action);
                        return Task.FromResult(new PipeReply(false,"ROUTING_UPDATE_UNCONFIRMED",observed));
                    }
                    return unavailable?Task.FromException<PipeReply>(new ClientFault("SERVICE_TIMEOUT"))
                        :Task.FromResult(new PipeReply(true,"OK",observed));
                };
                CheckPages(window,model); // No observation: saved switches cannot claim a route.
                model.PollAsync().GetAwaiter().GetResult();CheckPages(window,model);
                observed=new(TunnelPhase.Connected,DateTimeOffset.UtcNow,ProbePassed:true,ProtectionActive:true);
                model.PollAsync().GetAwaiter().GetResult();
                model.Applications[1].Enabled=true;
                AutoRoutingChecks.Settle(()=>mutations==1&&!model.IsBusy);
                Assert.True(model.IsConnected);Assert.True(model.RulesPending);CheckPages(window,model);
                unavailable=true;model.PollAsync().GetAwaiter().GetResult();
                Assert.False(model.IsConnected);Assert.True(model.RulesPending);CheckPages(window,model);
                Assert.Equal(1,mutations);
            }
        }
        finally
        {
            window.DataContext=previous;Text.Catalog.SetLanguage(languageBefore);
            if(previous is MainViewModel original)ThemeManager.Apply(original.Theme);
            Sta.Pump();
        }
    }
    // Owner decision: the switch alone shows the choice; no "Use VPN / No VPN" caption next to it.
    private static void CheckPages(MainWindow window,MainViewModel model)
    {
        foreach(var page in new[]{"connection","applications"})
        {
            model.Page=page;Sta.Pump();
            Assert.DoesNotContain(Sta.Descendants<TextBlock>(window),item=>item.Name=="AppRoutingChoice");
            var switches=Sta.Descendants<CheckBox>(window).Where(item=>item.DataContext is AppItemViewModel).ToArray();
            Assert.Equal(model.Applications.Count,switches.Length);
            foreach(var option in switches)
            {
                var app=Assert.IsType<AppItemViewModel>(option.DataContext);
                Assert.Equal(app.Enabled,option.IsChecked);
                Assert.Equal(Text.Catalog["RouteChoiceHint"],page=="connection"?AutomationProperties.GetHelpText(option):Assert.IsType<string>(option.ToolTip));
                var bounds=option.TransformToAncestor(window).TransformBounds(new Rect(option.RenderSize));
                Assert.InRange(bounds.Right,0,window.ActualWidth);
            }
            if(page=="connection")Assert.Contains(Sta.Descendants<TextBlock>(window),item=>item.Text==Text.Catalog["SelectedApps"]);
        }
    }
}
