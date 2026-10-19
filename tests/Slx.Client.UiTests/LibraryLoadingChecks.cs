using System.Windows;
using System.Windows.Controls;
using Slx.Client.Core;
using Slx.Client.Desktop.Presentation;
using Slx.Client.Desktop.ViewModels;
using Slx.Client.Desktop.Views;
using Xunit;
namespace Slx.Client.UiTests;
internal static class LibraryLoadingChecks
{
    public static void Run()
    {
        using var model=new MainViewModel(false){CatalogEntries=[]};
        var pending=new TaskCompletionSource<AppRule[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        model.DiscoveryFetchOverride=_=>pending.Task;
        model.CatalogFetchOverride=_=>Task.FromResult<IReadOnlyList<CatalogApplication>>([]);
        model.DiscoverCommand.Execute(null);
        var view=new QuickApplicationsView{DataContext=model};
        var window=new Window{Content=view,Width=900,Height=650,ShowInTaskbar=false,Left=-20000,Top=-20000};
        window.Show();Sta.Pump();
        try
        {
            var loading=Sta.Descendants<StackPanel>(view).Single(x=>x.Name=="LibraryLoading");
            var empty=Sta.Descendants<TextBlock>(view).Single(x=>x.Name=="LibraryEmpty");
            Assert.True(model.Discovering);Assert.True(loading.IsVisible);Assert.False(empty.IsVisible);
            Assert.True(Assert.Single(Sta.Descendants<ProgressBar>(loading)).IsIndeterminate);
            Assert.False(model.DiscoverCommand.CanExecute(null));
            UiScreenshots.Save(window,"slx-library-loading");
            pending.SetResult([new(Guid.NewGuid(),"Loaded app",@"C:\Synthetic\Loaded.exe")]);
            PumpUntilDone(model);
            Assert.False(loading.IsVisible);Assert.False(empty.IsVisible);Assert.True(model.DiscoverCommand.CanExecute(null));
            Assert.Contains(model.FilteredLibrary,x=>x.Name=="Loaded app");
            model.DiscoveryFetchOverride=_=>Task.FromException<AppRule[]>(new ClientFault("DISCOVERY_UNAVAILABLE"));
            model.DiscoverCommand.Execute(null);Sta.Pump();PumpUntilDone(model);
            Assert.False(loading.IsVisible);Assert.True(model.ShowMessage);
            Assert.Contains(model.FilteredLibrary,x=>x.Name=="Loaded app");
        }
        finally{window.Close();Sta.Pump();}
    }
    private static void PumpUntilDone(MainViewModel model)
    {
        var clock=System.Diagnostics.Stopwatch.StartNew();
        while(model.Discovering&&clock.Elapsed<TimeSpan.FromSeconds(3))Sta.Pump();
        Assert.False(model.Discovering);
    }
}
