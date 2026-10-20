using System.IO;
using Slx.Client.Core;
using Slx.Client.Windows;
using Slx.Client.Desktop.Localization;
namespace Slx.Client.Desktop.ViewModels;
public sealed partial class MainViewModel
{
    private Task? discoveryCacheLoad;
    private DateTimeOffset? discoveryObservedAt;
    private bool discoveryFromCache,discoveryFailed,discoveryCacheWriteFailed;
    internal Func<Task<ApplicationDiscoverySnapshot?>>? DiscoveryCacheLoadOverride {get;set;}
    internal Func<ApplicationDiscoverySnapshot,Task>? DiscoveryCacheSaveOverride {get;set;}
    internal Task WarmDiscoveryCacheAsync()=>discoveryCacheLoad??=LoadDiscoveryCacheAsync();
    public string LibraryStatusText=>Text.Catalog[Discovering?discoveryCancellation?.IsCancellationRequested==true?"LibrarySearchSlow":HasDiscovered?"LibraryRefreshing":"FindingApps"
        :discoveryFailed?HasDiscovered?"LibraryRefreshPartial":"DiscoveryUnavailable"
        :discoveryCacheWriteFailed?"LibraryCacheUnavailable":discoveryObservedAt is null?"LibraryReady":discoveryFromCache?"LibrarySaved":"LibraryUpdated"];
    public string LibraryObservationText=>discoveryObservedAt is {} at?at.ToLocalTime().ToString("g"):"";
    public bool HasLibraryObservation=>discoveryObservedAt!=null;
    private async Task LoadDiscoveryCacheAsync()
    {
        if(!persistPreferences&&DiscoveryCacheLoadOverride is null)return;
        try
        {
            var cached=await(DiscoveryCacheLoadOverride?.Invoke()??Task.Run(store.LoadDiscoveryCache));
            if(disposed||cached is null||lastDiscoverySuccess!=0)return;
            discoveryObservedAt=cached.CapturedAt;discoveryFromCache=true;
            ApplyDiscovery(cached.Applications.Concat(Discovered));
        }
        catch(Exception error) when(error is ClientFault or IOException or UnauthorizedAccessException)
        { /* Optional discovery cache never blocks settings, subscription or fresh discovery. */ }
    }
    private async Task SaveDiscoveryCacheAsync(ApplicationDiscoverySnapshot snapshot)
    {
        if(!persistPreferences&&DiscoveryCacheSaveOverride is null)return;
        try{await(DiscoveryCacheSaveOverride?.Invoke(snapshot)??Task.Run(()=>store.SaveDiscoveryCache(snapshot)));}
        catch(Exception error) when(error is ClientFault or IOException or UnauthorizedAccessException)
        {if(!disposed)discoveryCacheWriteFailed=true;}
    }
    private void NotifyLibraryState()
    {
        foreach(var name in new[]{nameof(LibraryStatusText),nameof(LibraryObservationText),nameof(HasLibraryObservation)})OnPropertyChanged(name);
    }
}
