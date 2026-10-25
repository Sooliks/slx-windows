using System.Diagnostics;
using Slx.Client.Core;
using Slx.Client.Desktop.ViewModels;
using Slx.Client.Windows;
using Xunit;
namespace Slx.Client.UiTests;
internal static class LibraryPriorityChecks
{
    public static void Run()
    {
        var selected=UiFixtures.Preferences(RoutingMode.SelectedApplications);
        using var model=new MainViewModel(false,UiFixtures.Subscription,selected);
        CatalogApplication recommendation=new("recommended-browser","A uninstalled browser","browser",["uninstalled.exe"]);
        model.CatalogEntries=[recommendation];
        model.CatalogFetchOverride=_=>Task.FromResult<IReadOnlyList<CatalogApplication>>([recommendation]);
        var browser=App("Z browser","chrome.exe");var telegram=App("Y Telegram","Telegram.exe");
        var discord=App("X Discord","discord.exe");var game=App("W game","cs2.exe");var tool=App("A editor","editor.exe");
        var cached=new[]{tool,game,discord,telegram,browser};
        var captured=DateTimeOffset.UtcNow.AddDays(-1);int commands=0,saves=0;
        model.SendOverride=(_,_)=>{commands++;throw new InvalidOperationException("Discovery must not send service commands");};
        model.DiscoveryCacheLoadOverride=()=>Task.FromResult<ApplicationDiscoverySnapshot?>(new(captured,cached));
        ApplicationDiscoverySnapshot? saved=null;
        model.DiscoveryCacheSaveOverride=snapshot=>{saves++;saved=snapshot;return Task.CompletedTask;};
        var completed=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Action<DiscoveryBatch>? publish=null;
        model.DiscoveryScanOverride=(_,callback,token)=>{publish=callback;return completed.Task.WaitAsync(token);};
        model.WarmDiscoveryCacheAsync().GetAwaiter().GetResult();
        var expected=new[]{browser.Name,telegram.Name,discord.Name,game.Name,tool.Name,recommendation.Name};
        Assert.Equal(expected,model.FilteredLibrary.Select(entry=>entry.Name));
        Assert.NotNull(model.FilteredLibrary[0].Rule);Assert.Null(model.FilteredLibrary[^1].Rule);
        Assert.Equal(captured.ToLocalTime().ToString("g"),model.LibraryObservationText);
        var originalBrowser=model.FilteredLibrary[0];var originalTool=model.FilteredLibrary[4];
        model.DiscoverCommand.Execute(null);Assert.NotNull(publish);
        var secondBrowser=App("B browser","firefox.exe");
        publish!(new(DiscoverySource.Running,[tool,secondBrowser],true));
        Until(()=>model.FilteredLibrary.Count==7);
        Assert.Equal(new[]{secondBrowser.Name}.Concat(expected),model.FilteredLibrary.Select(entry=>entry.Name));
        Assert.Same(originalBrowser,model.FilteredLibrary[1]);Assert.Same(originalTool,model.FilteredLibrary[5]);
        model.DiscoverySearch="editor";Assert.Same(originalTool,Assert.Single(model.FilteredLibrary));
        model.DiscoverySearch="uninstalled";Assert.Null(Assert.Single(model.FilteredLibrary).Rule);
        model.DiscoverySearch="";
        foreach(var source in Enum.GetValues<DiscoverySource>().Where(source=>source!=DiscoverySource.Running))
            publish(new(source,source==DiscoverySource.Installed?cached:[],true));
        completed.SetResult();Until(()=>!model.Discovering);
        Assert.Equal(new[]{secondBrowser.Name}.Concat(expected),model.FilteredLibrary.Select(entry=>entry.Name));
        Assert.Equal(1,saves);Assert.NotNull(saved);Assert.Equal(secondBrowser,saved.Applications[0]);
        Assert.DoesNotContain(saved.Applications,app=>app.Name==recommendation.Name);
        Assert.Equal(selected.Applications,model.Applications.SelectMany(item=>item.ToRules()).ToList());
        Assert.False(model.RulesPending);Assert.Equal(0,commands);
    }
    private static AppRule App(string name,string file)=>new(Guid.NewGuid(),name,@"C:\SLX Priority UI Fixture\"+file);
    private static void Until(Func<bool> complete)
    {
        var watch=Stopwatch.StartNew();while(!complete()&&watch.Elapsed<TimeSpan.FromSeconds(3))Sta.Pump();
        Assert.True(complete());
    }
}
