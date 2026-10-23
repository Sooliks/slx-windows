using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
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
                window.Width=language=="ru"?940:1200;window.Height=language=="ru"?660:820;
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
    private static void CheckPages(MainWindow window,MainViewModel model)
    {
        foreach(var page in new[]{"connection","applications"})
        {
            model.Page=page;Sta.Pump();
            var choices=Sta.Descendants<TextBlock>(window).Where(item=>item.Name=="AppRoutingChoice").ToArray();
            Assert.Equal(model.Applications.Count,choices.Length);
            foreach(var label in choices)
            {
                var app=Assert.IsType<AppItemViewModel>(label.DataContext);
                Assert.Equal(Text.Catalog[app.Enabled?"RouteViaVpn":"RouteDirect"],label.Text);
                Assert.Equal(Text.Catalog["RouteChoiceHint"],Assert.IsType<string>(label.ToolTip));
                Assert.Equal(Text.Catalog["RouteChoiceHint"],AutomationProperties.GetHelpText(label));
                Assert.True(label.FontSize>=13);
                var text=new FormattedText(label.Text,CultureInfo.CurrentUICulture,label.FlowDirection,
                    new Typeface(label.FontFamily,label.FontStyle,label.FontWeight,label.FontStretch),label.FontSize,
                    label.Foreground,VisualTreeHelper.GetDpi(label).PixelsPerDip);
                Assert.True(label.ActualWidth+2>=text.WidthIncludingTrailingWhitespace);
                var bounds=label.TransformToAncestor(window).TransformBounds(new Rect(label.RenderSize));
                Assert.InRange(bounds.Right,0,window.ActualWidth);
            }
            if(page=="connection")Assert.Contains(Sta.Descendants<TextBlock>(window),item=>item.Text==Text.Catalog["SelectedApps"]);
        }
    }
}
