using Microsoft.Win32;
using Slx.Client.Core;
using Slx.Client.Desktop.Localization;
using Slx.Client.Desktop.Presentation;
using Slx.Client.Windows;
namespace Slx.Client.Desktop.ViewModels;
public sealed partial class MainViewModel
{
    public string Language
    {
        get => preferences.Language;
        set { if (value is not ("ru" or "en") || value == Language) return; preferences = preferences with { Language = value }; Text.Catalog.SetLanguage(value); SavePreferences(); OnPropertyChanged(); }
    }
    public string Theme
    {
        get => preferences.Theme;
        set { if (value is not ("olive" or "terminal") || value == Theme) return; preferences = preferences with { Theme = value }; ThemeManager.Apply(value); SavePreferences(); OnPropertyChanged(); }
    }
    public bool AutoStart
    {
        get => preferences.AutoStart;
        set
        {
            if (value == AutoStart) return;
            if (HasPreferencesFault) { ShowFault("STORAGE_UNAVAILABLE"); OnPropertyChanged(); return; }
            if (!value) CancelStartupConnection();
            try
            {
                StartupRegistration.SetEnabled(value); preferences = preferences with { AutoStart = value };
                SavePreferences(); OnPropertyChanged();
            }
            catch (ClientFault fault) { ShowFault(fault.Code); }
        }
    }
    public bool AutoConnectOnStartup
    {
        get => preferences.AutoConnectOnStartup;
        set
        {
            if (value == AutoConnectOnStartup) return;
            preferences = preferences with { AutoConnectOnStartup = value };
            if (!value) CancelStartupConnection();
            SavePreferences(); OnPropertyChanged();
        }
    }
    private void ReconcileStartup()
    {
        if (!persistPreferences || HasPreferencesFault) return;
        try { StartupRegistration.SetEnabled(preferences.AutoStart); }
        catch (ClientFault fault) { ShowFault(fault.Code); }
    }
    public bool MinimizeToTray
    {
        get => preferences.MinimizeToTray;
        set { if (value == MinimizeToTray) return; preferences = preferences with { MinimizeToTray = value }; SavePreferences(); OnPropertyChanged(); }
    }
    public bool IsSelectedMode { get => preferences.Mode == RoutingMode.SelectedApplications; set { if (value) SetMode(RoutingMode.SelectedApplications); } }
    public bool IsWholeMode { get => preferences.Mode == RoutingMode.WholeComputer; set { if (value) SetMode(RoutingMode.WholeComputer); } }
    public string ModeDescription => Text.Catalog[IsSelectedMode ? "ModeSelectedDescription" : "ModeWholeDescription"];
    private void SetMode(RoutingMode value)
    {
        if (preferences.Mode == value) return;
        preferences = preferences with { Mode = value }; SavePreferences(); MarkConnectionChanged();
        OnPropertyChanged(nameof(IsSelectedMode)); OnPropertyChanged(nameof(IsWholeMode)); OnPropertyChanged(nameof(ModeDescription));
    }
    private void Attach(IEnumerable<AppRule> rules)
    {
        var item = new AppItemViewModel(rules);
        item.RuleChanged += (_, _) => { SaveApplications(); MarkRulesChanged(); };
        Applications.Add(item);
    }
    private void SaveApplications()
    {
        preferences = preferences with { Applications = Applications.SelectMany(x => x.ToRules()).ToList() };
        SavePreferences();
        foreach (var name in new[] { nameof(SelectedAppCount), nameof(FilteredApplications), nameof(HasApplications), nameof(HasSelectedApplications), nameof(HasSearchResults) }) OnPropertyChanged(name);
        RefreshDiscoveryProperties();
    }

    private void AddExecutable()
    {
        var picker = new OpenFileDialog { Filter = Text.Catalog["ExecutableFilter"], CheckFileExists = true, Multiselect = false };
        if (picker.ShowDialog() != true) return;
        try { AddApplicationPaths([AppDiscovery.FromExecutable(picker.FileName)]); }
        catch (ClientFault fault) { ShowFault(fault.Code); }
    }
    private void AddApplicationPaths(IReadOnlyList<AppRule> paths)
    {
        var rule=ApplicationGroups.Normalize(paths[0]);
        paths=paths.Select(ApplicationGroups.Normalize).Select(x=>x with{Name=rule.Name,Enabled=true})
            .DistinctBy(x=>x.ExecutablePath,StringComparer.OrdinalIgnoreCase).ToArray();
        var existing=Applications.FirstOrDefault(x=>string.Equals(x.Name.Trim(),rule.Name.Trim(),StringComparison.CurrentCultureIgnoreCase));
        if(existing!=null){foreach(var path in paths)existing.AddPath(path);existing.Enabled=true;}
        else Attach(paths.Select(x=>x with{Enabled=true}));
        SaveApplications();MarkRulesChanged();
        RefreshDiscoveryProperties();
    }
    private void RemoveApp(AppItemViewModel? item)
    {
        if (item == null) return;
        Applications.Remove(item); SaveApplications(); MarkRulesChanged();
    }
    private void SavePreferences()
    {
        if (!persistPreferences) return;
        try { store.SavePreferences(preferences); }
        catch (ClientFault fault) { ShowFault(fault.Code); }
    }
}
