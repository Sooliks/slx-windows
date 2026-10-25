using System.Collections.Concurrent;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Slx.Client.Core;
using Slx.Client.Windows;
namespace Slx.Client.Desktop.ViewModels;
public sealed partial class MainViewModel
{
    internal Func<CancellationToken,Task<AppRule[]>>? DiscoveryFetchOverride {get;set;}
    internal Func<IReadOnlyList<CatalogApplication>,Action<DiscoveryBatch>,CancellationToken,Task>? DiscoveryScanOverride {get;set;}
    internal Func<CancellationToken,Task<IReadOnlyList<CatalogApplication>>>? CatalogFetchOverride {get;set;}
    private bool discovering, changingDiscovery;
    private long lastDiscoverySuccess;
    private CancellationTokenSource? discoveryCancellation;
    public bool Discovering {get=>discovering;private set{if(SetProperty(ref discovering,value)){DiscoverCommand.NotifyCanExecuteChanged();NotifyLibraryState();}}}
    internal Task RefreshLibraryIfNeededAsync(){RefreshMissingIcons();return DiscoverApplicationsAsync(false);}
    private async Task DiscoverApplicationsAsync(bool force = true)
    {
        if(Discovering||disposed||!force&&lastDiscoverySuccess!=0&&System.Diagnostics.Stopwatch.GetElapsedTime(lastDiscoverySuccess)<TimeSpan.FromMinutes(5))return;
        if(force)RefreshMissingIcons();
        Discovering=true; discoveryFailed=false;discoveryCacheWriteFailed=false;
        using var current=new CancellationTokenSource(TimeSpan.FromSeconds(12));
        discoveryCancellation=current;
        bool acceptingProgress=true;
        var dispatcher=Application.Current.Dispatcher;
        using var expiration=current.Token.Register(()=>{if(!disposed&&!dispatcher.HasShutdownStarted)_=dispatcher.BeginInvoke(new Action(()=>{if(!disposed)NotifyLibraryState();}));});
        try
        {
            await WarmDiscoveryCacheAsync();
            if(disposed||current.IsCancellationRequested)return;
            var baseline=Discovered.ToArray();
            var batches=new ConcurrentDictionary<DiscoverySource,DiscoveryBatch>();
            void Publish(DiscoveryBatch batch)
            {
                if(disposed||current.IsCancellationRequested||dispatcher.HasShutdownStarted)return;
                batches[batch.Source]=batch;
                _=dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>
                {
                    if(acceptingProgress&&!disposed&&!current.IsCancellationRequested&&ReferenceEquals(discoveryCancellation,current))
                        ApplyDiscovery(MergeWithPrior(baseline,MergedBatches(batches.Values)),true);
                }));
            }
            async Task Scan()
            {
                if(DiscoveryFetchOverride is not null)Publish(new(DiscoverySource.Running,await DiscoveryFetchOverride(current.Token),true));
                else await (DiscoveryScanOverride?.Invoke(CatalogEntries,Publish,current.Token)??DiscoveryScanner.RunAsync(CatalogEntries,Publish,current.Token));
            }
            await Task.WhenAll(Scan(),RefreshLibraryCatalogAsync(current.Token));
            acceptingProgress=false;
            if(disposed||current.IsCancellationRequested)return;
            int expected=DiscoveryFetchOverride is null?Enum.GetValues<DiscoverySource>().Length:1;
            bool complete=batches.Count==expected&&batches.Values.All(x=>x.Success);
            var fresh=MergedBatches(batches.Values).ToArray();
            ApplyDiscovery(complete?fresh:MergeWithPrior(baseline,fresh),!complete);
            discoveryFailed=!complete;
            if(complete)
            {
                discoveryObservedAt=DateTimeOffset.UtcNow;discoveryFromCache=false;
                lastDiscoverySuccess=System.Diagnostics.Stopwatch.GetTimestamp();
                await SaveDiscoveryCacheAsync(new(discoveryObservedAt.Value,fresh));
            }
        }
        catch(Exception error) when(error is ClientFault or System.ComponentModel.Win32Exception or IOException or UnauthorizedAccessException or OperationCanceledException)
        {if(!disposed)discoveryFailed=true;}
        finally
        {
            acceptingProgress=false;
            if(ReferenceEquals(discoveryCancellation,current))discoveryCancellation=null;
            Discovering=false;
        }
    }
    private IEnumerable<AppRule> MergedBatches(IEnumerable<DiscoveryBatch> batches)=>new LibraryApplicationOrder(CatalogEntries).Order(batches.Where(x=>x.Success)
        .OrderBy(x=>x.Source).SelectMany(x=>x.Apps).DistinctBy(x=>x.ExecutablePath,StringComparer.OrdinalIgnoreCase)).Take(300);
    private static IEnumerable<AppRule> MergeWithPrior(IEnumerable<AppRule> baseline,IEnumerable<AppRule> fresh)
    {
        var latest=fresh.ToDictionary(x=>x.ExecutablePath,StringComparer.OrdinalIgnoreCase);
        foreach(var previous in baseline)yield return latest.Remove(previous.ExecutablePath,out var updated)?updated:previous;
        foreach(var added in latest.Values)yield return added;
    }
    private void ApplyDiscovery(IEnumerable<AppRule> rows,bool preservePrior=false)
    {
        var unique=rows.DistinctBy(x=>x.ExecutablePath,StringComparer.OrdinalIgnoreCase);
        int maximum=preservePrior?600:300;
        if(preservePrior)unique=unique.Take(maximum);
        var found=new LibraryApplicationOrder(CatalogEntries).Order(unique).Take(maximum).ToArray();
        changingDiscovery=true;
        try{Discovered.Clear();foreach(var row in found)Discovered.Add(row);}
        finally{changingDiscovery=false;}
        RefreshDiscoveryProperties();
    }
    private async Task RefreshLibraryCatalogAsync(CancellationToken token)
    {
        try
        {
            var fresh=await(CatalogFetchOverride?.Invoke(token)??CatalogClient.RefreshAsync(token));
            if(!disposed&&!token.IsCancellationRequested)CatalogEntries=fresh;
        }
        catch(Exception error) when(error is ClientFault or System.Net.Http.HttpRequestException or OperationCanceledException or IOException)
        { /* Bundled recommendations remain separate from locally discovered applications. */ }
    }
}
