using System.IO;
using Microsoft.Win32;
using Slx.Client.Core;
using Slx.Client.Windows;
using Slx.Client.Desktop.Presentation;
namespace Slx.Client.Desktop.ViewModels;
public sealed partial class MainViewModel
{
    private CancellationTokenSource? libraryAddCancellation;
    internal Func<IReadOnlyList<AppRule>,CancellationToken,Task<IReadOnlyList<AppRule>>>? LibraryValidateOverride {get;set;}
    private async Task AddLibraryAsync(LibraryEntry? entry)
    {
        if(entry is null||disposed||!CanEditRouting||libraryAddCancellation is not null)return;
        if(entry.Rule is null)
        {
            var picker=new OpenFileDialog{Filter=Localization.Text.Catalog["ExecutableFilter"],CheckFileExists=true,Multiselect=false,
                Title=entry.Name,FileName=entry.Catalog?.Executables.FirstOrDefault()??""};
            if(picker.ShowDialog()==true)
                try{AddApplicationPaths([AppDiscovery.FromExecutable(picker.FileName)]);}
                catch(ClientFault fault){ShowFault(fault.Code);}
            return;
        }
        using var current=new CancellationTokenSource(TimeSpan.FromSeconds(8));libraryAddCancellation=current;
        try
        {
            var paths=entry.Paths??[entry.Rule];
            var found=await(LibraryValidateOverride?.Invoke(paths,current.Token)??Task.Run<IReadOnlyList<AppRule>>(()=>
            {
                var valid=new List<AppRule>();
                foreach(var path in paths){current.Token.ThrowIfCancellationRequested();if(PlanValidator.IsExecutablePath(path.ExecutablePath)&&File.Exists(path.ExecutablePath))valid.Add(path);}
                return valid;
            },current.Token));
            if(disposed||current.IsCancellationRequested||!CanEditRouting)return;
            if(found.Count==0){Notice("LibraryAppUnavailable");_=DiscoverApplicationsAsync();return;}
            AddApplicationPaths(found);
        }
        catch(Exception error) when(error is ClientFault or IOException or UnauthorizedAccessException or OperationCanceledException)
        {if(!disposed)Notice("LibraryAppUnavailable");}
        finally{if(ReferenceEquals(libraryAddCancellation,current))libraryAddCancellation=null;}
    }
    private async Task AddDiscoveredAsync(AppRule? rule)
    {
        if(rule is null)return;
        rule=ApplicationGroups.Normalize(rule);
        var paths=Discovered.Where(x=>string.Equals(x.Name.Trim(),rule.Name.Trim(),StringComparison.CurrentCultureIgnoreCase))
            .Append(rule).DistinctBy(x=>x.ExecutablePath,StringComparer.OrdinalIgnoreCase).ToArray();
        using var entry=new LibraryEntry(rule.Name,rule,null,paths);
        await AddLibraryAsync(entry);
    }
}
