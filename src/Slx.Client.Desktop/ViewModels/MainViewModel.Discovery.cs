using Slx.Client.Core;
using Slx.Client.Windows;
namespace Slx.Client.Desktop.ViewModels;
public sealed partial class MainViewModel
{
    private string discoverySearch = "";
    private bool discovering;
    public bool Discovering { get => discovering; private set { if (SetProperty(ref discovering, value)) DiscoverCommand.NotifyCanExecuteChanged(); } }
    public string DiscoverySearch { get => discoverySearch; set { if (SetProperty(ref discoverySearch, value)) RefreshDiscoveryProperties(); } }
    public IEnumerable<AppRule> FilteredDiscovered => Discovered.Where(app =>
        !Applications.Any(added => string.Equals(added.ExecutablePath, app.ExecutablePath, StringComparison.OrdinalIgnoreCase))
        && app.Name.Contains(DiscoverySearch, StringComparison.CurrentCultureIgnoreCase));
    public bool HasDiscoveryResults => FilteredDiscovered.Any();
    private async Task DiscoverApplicationsAsync()
    {
        if (Discovering || disposed) return;
        Discovering = true;
        try
        {
            var apps = await Task.Run(AppDiscovery.RunningApplications);
            if (disposed) return;
            Discovered.Clear();
            foreach (var app in apps) Discovered.Add(app);
            RefreshDiscoveryProperties();
        }
        catch (Exception error) when (error is ClientFault or System.ComponentModel.Win32Exception or System.IO.IOException or UnauthorizedAccessException)
        { Notice("DiscoveryUnavailable"); }
        finally { Discovering = false; }
    }
    private void RefreshDiscoveryProperties()
    {
        OnPropertyChanged(nameof(FilteredDiscovered));
        OnPropertyChanged(nameof(HasDiscoveryResults));
        OnPropertyChanged(nameof(HasDiscovered));
    }
}
