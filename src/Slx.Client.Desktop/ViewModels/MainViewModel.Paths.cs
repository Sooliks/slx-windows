using Slx.Client.Windows;
namespace Slx.Client.Desktop.ViewModels;
public sealed partial class MainViewModel
{
    private async Task RepairVersionedApplicationsAsync(CancellationToken cancellationToken)
    {
        var source = Applications.Select(app => (App: app, Rules: app.ToRules().ToArray())).ToArray();
        if (!source.SelectMany(item => item.Rules).Any(rule => VersionedApplicationPaths.NeedsRefresh(rule)
            || ApplicationGroups.NeedsRefresh(rule))) return;
        var repairs = await Task.Run(() => source.Select(item =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var versioned = VersionedApplicationPaths.Refresh(item.Rules);
            return (item.App, Rules: ApplicationGroups.Refresh(versioned, cancellationToken));
        }).ToArray(), cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (disposed) return;
        var changed = false;
        foreach (var repair in repairs) changed |= repair.App.ApplyRefreshedPaths(repair.Rules);
        if (changed) SaveApplications();
    }
}
