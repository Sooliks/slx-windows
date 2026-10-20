using System.Collections.ObjectModel;
using Slx.Client.Core;
using Slx.Client.Windows;
using Slx.Client.Desktop.Presentation;
namespace Slx.Client.Desktop.ViewModels;
public sealed partial class MainViewModel
{
    private string discoverySearch="";
    private int libraryLimit=12;
    public bool HasMoreLibraryResults {get;private set;}
    private IReadOnlyList<CatalogApplication> catalogEntries=CatalogClient.Bundled();
    private readonly Dictionary<string,LibraryEntry> libraryEntries=new(StringComparer.CurrentCultureIgnoreCase);
    internal IReadOnlyList<CatalogApplication> CatalogEntries {get=>catalogEntries;set{catalogEntries=value;RefreshDiscoveryProperties();}}
    public string DiscoverySearch {get=>discoverySearch;set{if(SetProperty(ref discoverySearch,value)){libraryLimit=12;RefreshDiscoveryProperties();}}}
    public IEnumerable<AppRule> FilteredDiscovered=>Discovered.Where(app=>
        !Applications.Any(added=>string.Equals(added.Name.Trim(),app.Name.Trim(),StringComparison.CurrentCultureIgnoreCase))
        &&app.Name.Contains(DiscoverySearch,StringComparison.CurrentCultureIgnoreCase)).GroupBy(x=>x.Name.Trim(),StringComparer.CurrentCultureIgnoreCase).Select(x=>x.First());
    public ObservableCollection<LibraryEntry> FilteredLibrary {get;}=[];
    public bool HasDiscoveryResults=>FilteredLibrary.Count>0;
    private void RefreshDiscoveryProperties()
    {
        var desired=new List<LibraryEntry>();var retained=new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
        var names=Discovered.Concat(Applications.SelectMany(x=>x.ToRules())).Select(x=>System.IO.Path.GetFileName(x.ExecutablePath)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach(var group in Discovered.GroupBy(x=>x.Name.Trim(),StringComparer.CurrentCultureIgnoreCase))
        {
            if(Applications.Any(x=>string.Equals(x.Name.Trim(),group.Key,StringComparison.CurrentCultureIgnoreCase)))continue;
            var paths=group.ToArray();string key="app:"+group.Key;retained.Add(key);
            if(!libraryEntries.TryGetValue(key,out var entry)||entry.Name!=group.Key||entry.Paths is null)
            {entry?.Dispose();libraryEntries[key]=entry=new(group.Key,paths[0],null,paths);}
            else if(!entry.Paths.Select(x=>x.ExecutablePath).SequenceEqual(paths.Select(x=>x.ExecutablePath),StringComparer.OrdinalIgnoreCase))entry.UpdatePaths(paths[0],paths);
            desired.Add(entry);
        }
        foreach(var app in CatalogEntries.Where(app=>!app.Executables.Any(names.Contains)))
        {
            string key="catalog:"+app.Id;retained.Add(key);
            if(!libraryEntries.TryGetValue(key,out var entry)||entry.Catalog!=app)
            {entry?.Dispose();libraryEntries[key]=entry=new(app.Name,null,app);}
            desired.Add(entry);
        }
        foreach(var key in libraryEntries.Keys.Where(key=>!retained.Contains(key)).ToArray())
        {libraryEntries[key].Dispose();libraryEntries.Remove(key);}
        var matching=desired.Where(x=>x.Name.Contains(DiscoverySearch,StringComparison.CurrentCultureIgnoreCase)).ToArray();
        HasMoreLibraryResults=matching.Length>libraryLimit;OnPropertyChanged(nameof(HasMoreLibraryResults));
        var visible=matching.Take(libraryLimit).ToArray();
        for(int i=0;i<visible.Length;i++)
        {
            if(i<FilteredLibrary.Count&&ReferenceEquals(FilteredLibrary[i],visible[i]))continue;
            int prior=FilteredLibrary.IndexOf(visible[i]);
            if(prior>=0)FilteredLibrary.Move(prior,i);else FilteredLibrary.Insert(i,visible[i]);
        }
        while(FilteredLibrary.Count>visible.Length)FilteredLibrary.RemoveAt(FilteredLibrary.Count-1);
        OnPropertyChanged(nameof(FilteredDiscovered));OnPropertyChanged(nameof(HasDiscoveryResults));OnPropertyChanged(nameof(HasDiscovered));
        NotifyLibraryState();
    }
    private void ShowMoreApplications(){libraryLimit+=12;RefreshDiscoveryProperties();}
}
