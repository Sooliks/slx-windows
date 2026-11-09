using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Slx.Client.Core;
using Slx.Client.Desktop.Presentation;
using Slx.Client.Desktop.Localization;
using Slx.Client.Windows;
namespace Slx.Client.Desktop.ViewModels;
public sealed class AppItemViewModel : ObservableObject
{
    private bool enabled;
    private readonly string ruleName;
    private readonly List<AppRule> members=[];
    private LocalIconRequest? iconRequest;
    private string[] sharedWith = [], includedFiles = [];
    public string GroupDescription => ApplicationLabels.IsRiotClient(ExecutablePath) ? Text.Catalog["RiotSharedClient"]
        : includedFiles.Length > 1 ? Text.Catalog["IncludedExecutables"] + " " + string.Join(", ", includedFiles) : "";
    public string SharedWarning => !Enabled && sharedWith.Length > 0
        ? Text.Catalog["EnabledByOtherGroup"] + " " + string.Join(", ", sharedWith) : "";
    public void RefreshComposition(IReadOnlyList<AppRule> expanded, IEnumerable<string> owners)
    {
        includedFiles = expanded.Select(x => System.IO.Path.GetFileName(x.ExecutablePath)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        sharedWith = owners.Distinct(StringComparer.CurrentCultureIgnoreCase).ToArray();
        OnPropertyChanged(nameof(GroupDescription)); OnPropertyChanged(nameof(SharedWarning));
    }
    public AppItemViewModel(AppRule rule):this(new[]{rule}){}
    public AppItemViewModel(IEnumerable<AppRule> rules)
    {
        var items=rules.ToArray();var primary=items[0];
        Id=primary.Id;ruleName=primary.Name;Name=ApplicationLabels.DisplayName(primary);ExecutablePath=primary.ExecutablePath;enabled=items.Any(x=>x.Enabled);
        foreach(var rule in items)AddPath(rule);
        RefreshIcon();
    }
    public Guid Id { get; }
    public string Name { get; }
    public string ExecutablePath { get; private set; }
    public string ExecutablePaths=>string.Join(Environment.NewLine,members.Select(x=>x.ExecutablePath));
    public ImageSource? Icon { get; private set; }
    public bool Enabled { get=>enabled;set{if(SetProperty(ref enabled,value))RuleChanged?.Invoke(this,EventArgs.Empty);} }
    public event EventHandler? RuleChanged;
    public bool AddPath(AppRule rule)
    {
        if(members.Any(x=>string.Equals(x.ExecutablePath,rule.ExecutablePath,StringComparison.OrdinalIgnoreCase)))return false;
        members.Add(rule);OnPropertyChanged(nameof(ExecutablePaths));
        if(iconRequest is not null)RefreshIcon();
        return true;
    }
    public bool ApplyRefreshedPaths(IReadOnlyList<AppRule> refreshed)
    {
        if (members.Select(x => x.ExecutablePath).SequenceEqual(refreshed.Select(x => x.ExecutablePath), StringComparer.OrdinalIgnoreCase)) return false;
        members.Clear(); members.AddRange(refreshed);
        ExecutablePath = members[0].ExecutablePath;
        RefreshIcon();
        OnPropertyChanged(nameof(ExecutablePath)); OnPropertyChanged(nameof(ExecutablePaths));
        return true;
    }
    public AppRule ToRule()=>new(Id,ruleName,ExecutablePath,Enabled);
    public IEnumerable<AppRule> ToRules()=>members.Select(x=>x with{Name=ruleName,Enabled=Enabled});
    internal void RefreshIcon()
    {
        var paths = members.Select(member => member.ExecutablePath).ToArray();
        iconRequest?.Dispose(); Icon = LocalIcon.Cached(paths); OnPropertyChanged(nameof(Icon));
        iconRequest = LocalIconRequest.Start(this, paths, static (item, image) =>
        { item.Icon = image; item.OnPropertyChanged(nameof(Icon)); });
    }
}
