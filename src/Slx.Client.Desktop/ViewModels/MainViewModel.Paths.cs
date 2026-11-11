using Slx.Client.Windows;
using Slx.Client.Core;
namespace Slx.Client.Desktop.ViewModels;
public sealed partial class MainViewModel
{
    private async Task RepairVersionedApplicationsAsync(CancellationToken cancellationToken)
    {
        var source = Applications.Select(app => (App: app, Rules: app.ToRules().ToArray())).ToArray();
        if (!source.SelectMany(item => item.Rules).Any(NeedsPathRefresh)) return;
        var repairs = await Task.Run(() => source.Select(item =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return (item.App, Rules: ResolveKnownPaths(item.Rules, cancellationToken));
        }).ToArray(), cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (disposed) return;
        var changed = false;
        foreach (var repair in repairs) changed |= repair.App.ApplyRefreshedPaths(repair.Rules);
        if (changed) SaveApplications();
    }
    private static bool NeedsPathRefresh(AppRule rule) =>
        VersionedApplicationPaths.NeedsRefresh(rule) || ApplicationGroups.NeedsRefresh(rule);
    private static IReadOnlyList<AppRule> ResolveKnownPaths(IReadOnlyList<AppRule> rules, CancellationToken token) =>
        ApplicationGroups.Refresh(VersionedApplicationPaths.Refresh(rules), token);
}
