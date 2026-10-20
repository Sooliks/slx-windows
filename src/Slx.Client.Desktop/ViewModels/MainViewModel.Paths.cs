using Slx.Client.Windows;
namespace Slx.Client.Desktop.ViewModels;
public sealed partial class MainViewModel
{
    private async Task RepairVersionedApplicationsAsync(CancellationToken cancellationToken)
    {
        var source = Applications.Select(app => (App: app, Rules: app.ToRules().ToArray())).ToArray();
        if (!source.SelectMany(item => item.Rules).Any(VersionedApplicationPaths.NeedsRefresh)) return;
        var repairs = await Task.Run(() => source.Select(item =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return (item.App, Rules: VersionedApplicationPaths.Refresh(item.Rules));
        }).ToArray(), cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (disposed) return;
        var changed = false;
        foreach (var repair in repairs) changed |= repair.App.ApplyRefreshedPaths(repair.Rules);
        if (changed) SaveApplications();
    }
}
