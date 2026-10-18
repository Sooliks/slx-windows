using Slx.Client.Core;
using Slx.Client.Windows;
using Slx.Client.Desktop.Presentation;
using Microsoft.Win32;
namespace Slx.Client.Desktop.ViewModels;
public sealed partial class MainViewModel
{
    private string discoverySearch="";
    private bool discovering;
    private CancellationTokenSource? discoveryCancellation;
    internal IReadOnlyList<CatalogApplication> CatalogEntries {get;set;}=CatalogClient.Bundled();
    public bool Discovering {get=>discovering;private set{if(SetProperty(ref discovering,value))DiscoverCommand.NotifyCanExecuteChanged();}}
    public string DiscoverySearch {get=>discoverySearch;set{if(SetProperty(ref discoverySearch,value))RefreshDiscoveryProperties();}}
    public IEnumerable<AppRule> FilteredDiscovered=>Discovered.Where(app=>
        !Applications.Any(added=>string.Equals(added.ExecutablePath,app.ExecutablePath,StringComparison.OrdinalIgnoreCase))
        &&app.Name.Contains(DiscoverySearch,StringComparison.CurrentCultureIgnoreCase));
    public IEnumerable<LibraryEntry> FilteredLibrary
    {
        get
        {
            var names=Discovered.Select(x=>System.IO.Path.GetFileName(x.ExecutablePath))
                .Concat(Applications.Select(x=>System.IO.Path.GetFileName(x.ExecutablePath))).ToHashSet(StringComparer.OrdinalIgnoreCase);
            return FilteredDiscovered.Select(app=>new LibraryEntry(app.Name,app,null))
                .Concat(CatalogEntries.Where(app=>!app.Executables.Any(names.Contains)
                    &&app.Name.Contains(DiscoverySearch,StringComparison.CurrentCultureIgnoreCase))
                    .Select(app=>new LibraryEntry(app.Name,null,app)));
        }
    }
    public bool HasDiscoveryResults=>FilteredLibrary.Any();
    private void AddLibrary(LibraryEntry? entry)
    {
        if(entry==null)return;
        if(entry.Rule!=null){AddDiscovered(entry.Rule);return;}
        var picker=new OpenFileDialog{Filter=Localization.Text.Catalog["ExecutableFilter"],CheckFileExists=true,Multiselect=false,
            Title=entry.Name,FileName=entry.Catalog?.Executables.FirstOrDefault()??""};
        if(picker.ShowDialog()!=true)return;
        try{AddDiscovered(AppDiscovery.FromExecutable(picker.FileName));}
        catch(ClientFault fault){ShowFault(fault.Code);}
    }
    private async Task DiscoverApplicationsAsync()
    {
        if(Discovering||disposed)return;
        Discovering=true;
        using var current=new CancellationTokenSource(TimeSpan.FromSeconds(8));
        discoveryCancellation=current;
        try
        {
            var apps=await Task.Run(()=>AppDiscovery.RunningApplications().Concat(AppDiscovery.InstalledApplications())
                .Concat(CatalogApplications.Find(CatalogEntries)).DistinctBy(app=>app.ExecutablePath,StringComparer.OrdinalIgnoreCase)
                .OrderBy(app=>app.Name,StringComparer.CurrentCultureIgnoreCase).Take(300).ToArray(),current.Token);
            if(disposed||current.IsCancellationRequested)return;
            Discovered.Clear();foreach(var app in apps)Discovered.Add(app);RefreshDiscoveryProperties();
            try
            {
                var fresh=await CatalogClient.RefreshAsync(current.Token);
                if(disposed||current.IsCancellationRequested)return;
                CatalogEntries=fresh;RefreshDiscoveryProperties();
            }
            catch(Exception error) when(error is ClientFault or System.Net.Http.HttpRequestException or OperationCanceledException or System.IO.IOException)
            { /* The available local catalog remains; no claim that refresh succeeded. */ }
        }
        catch(Exception error) when(error is ClientFault or System.ComponentModel.Win32Exception or System.IO.IOException or UnauthorizedAccessException or OperationCanceledException)
        {if(!disposed&&!current.IsCancellationRequested)Notice("DiscoveryUnavailable");}
        finally{if(ReferenceEquals(discoveryCancellation,current))discoveryCancellation=null;Discovering=false;}
    }
    private void RefreshDiscoveryProperties()
    {
        OnPropertyChanged(nameof(FilteredDiscovered));OnPropertyChanged(nameof(FilteredLibrary));
        OnPropertyChanged(nameof(HasDiscoveryResults));OnPropertyChanged(nameof(HasDiscovered));
    }
}
