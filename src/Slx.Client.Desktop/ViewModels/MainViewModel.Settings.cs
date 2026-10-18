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
            try { StartupRegistration.SetEnabled(value); preferences = preferences with { AutoStart = value }; SavePreferences(); OnPropertyChanged(); }
            catch (ClientFault fault) { ShowFault(fault.Code); }
        }
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
        preferences = preferences with { Mode = value }; SavePreferences(); MarkRulesChanged();
        OnPropertyChanged(nameof(IsSelectedMode)); OnPropertyChanged(nameof(IsWholeMode)); OnPropertyChanged(nameof(ModeDescription));
    }
    private void Attach(AppRule rule)
    {
        var item = new AppItemViewModel(rule);
        item.RuleChanged += (_, _) => { SaveApplications(); MarkRulesChanged(); };
        Applications.Add(item);
    }
    private void SaveApplications()
    {
        preferences = preferences with { Applications = Applications.Select(x => x.ToRule()).ToList() };
        SavePreferences();
        foreach (var name in new[] { nameof(FilteredApplications), nameof(SelectedApplications), nameof(HasApplications), nameof(HasSelectedApplications), nameof(HasSearchResults) }) OnPropertyChanged(name);
    }
    private void MarkRulesChanged() { if (IsTunnelActive) RulesPending = true; }
    private void AddExecutable()
    {
        var picker = new OpenFileDialog { Filter = Text.Catalog["ExecutableFilter"], CheckFileExists = true, Multiselect = false };
        if (picker.ShowDialog() != true) return;
        try { AddDiscovered(AppDiscovery.FromExecutable(picker.FileName)); }
        catch (ClientFault fault) { ShowFault(fault.Code); }
    }
    private void DiscoverApplications()
    {
        Discovered.Clear();
        try { foreach (var app in AppDiscovery.RunningApplications()) if (!Applications.Any(x => string.Equals(x.ExecutablePath, app.ExecutablePath, StringComparison.OrdinalIgnoreCase))) Discovered.Add(app); }
        catch (ClientFault fault) { ShowFault(fault.Code); }
        DiscoveryVisible = true; OnPropertyChanged(nameof(HasDiscovered)); OnPropertyChanged(nameof(IsOverlayVisible));
    }
    private void AddDiscovered(AppRule? rule)
    {
        if (rule == null) return;
        var existing = Applications.FirstOrDefault(x => string.Equals(x.ExecutablePath, rule.ExecutablePath, StringComparison.OrdinalIgnoreCase));
        if (existing != null) existing.Enabled = true;
        else { Attach(rule with { Enabled = true }); SaveApplications(); MarkRulesChanged(); }
        DiscoveryVisible = false; OnPropertyChanged(nameof(IsOverlayVisible));
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
