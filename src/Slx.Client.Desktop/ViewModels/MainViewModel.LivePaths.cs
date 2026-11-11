using System.IO;
using System.Windows;
using System.Windows.Threading;
using Slx.Client.Core;
using Slx.Client.Windows;

namespace Slx.Client.Desktop.ViewModels;

public sealed partial class MainViewModel
{
    private FileSystemWatcher? claudeCodeWatcher, claudeDesktopWatcher;
    private CancellationTokenSource? applicationPathRefresh;
    private int claudeWatchQueued;

    private async Task RefreshSelectedApplicationPathsAsync()
    {
        if (disposed || !persistPreferences) return;
        var watching = IsSelectedMode && IsTunnelActive;
        if (!watching || !Applications.Any(app => app.Enabled && ApplicationGroups.IsClaude(app.ToRule())))
            StopClaudeWatchers();
        if (!watching || !CanEditRouting || !IsConnected || RulesPending || routingDelay is not null
            || applicationPathRefresh is not null) return;
        var source = Applications.Select(app => (App: app, Rules: app.ToRules().ToArray())).ToArray();
        if (!source.Any(item => item.App.Enabled && item.Rules.Any(NeedsPathRefresh))) return;
        if (source.Any(item => item.App.Enabled && item.Rules.Any(ApplicationGroups.IsClaude)))
        {
            var dispatcher = Application.Current.Dispatcher;
            void Changed()
            {
                if (dispatcher.HasShutdownStarted || Interlocked.Exchange(ref claudeWatchQueued, 1) != 0) return;
                _ = dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                {
                    Interlocked.Exchange(ref claudeWatchQueued, 0);
                    _ = RefreshSelectedApplicationPathsAsync();
                }));
            }
            claudeCodeWatcher ??= ApplicationGroups.WatchClaudeCode(Changed);
            claudeDesktopWatcher ??= ApplicationGroups.WatchClaudeDesktop(Changed);
        }
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        applicationPathRefresh = budget;
        var owner = generation;
        var revision = routingChangeVersion;
        try
        {
            var after = await Task.Run(() =>
            {
                var proposed = source.Select(item => (item.App,
                    Rules: item.Rules.Any(rule => rule.Enabled && NeedsPathRefresh(rule))
                        ? ResolveKnownPaths(item.Rules, budget.Token) : item.Rules)).ToArray();
                if (proposed.Where((item, index) => !item.Rules.SequenceEqual(source[index].Rules)).Any())
                {
                    var flat = proposed.SelectMany(item => item.Rules).ToArray();
                    if (flat.Length > 128) throw new ClientFault("INVALID_APPLICATIONS");
                    _ = ApplicationGroups.Expand(flat);
                }
                return proposed;
            }, budget.Token);
            if (disposed || budget.IsCancellationRequested || generation != owner || routingChangeVersion != revision
                || !CanEditRouting || !IsConnected || RulesPending || routingDelay is not null || !IsSelectedMode
                || !Applications.SequenceEqual(source.Select(item => item.App))
                || source.Any(item => !item.Rules.SequenceEqual(item.App.ToRules()))) return;
            bool changed = false;
            foreach (var repair in after) changed |= repair.App.ApplyRefreshedPaths(repair.Rules);
            if (changed)
            {
                SaveApplications();
                MarkRulesChanged(); // Existing acknowledged WFP/Xray update, no reconnect.
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ClientFault or OperationCanceledException)
        { /* A missing, racing or over-budget group never replaces the active plan. The next status poll can rescan. */ }
        finally { applicationPathRefresh = null; }
    }

    private void StopApplicationPathRefresh()
    {
        applicationPathRefresh?.Cancel();
        StopClaudeWatchers();
    }
    private void StopClaudeWatchers()
    {
        claudeCodeWatcher?.Dispose();
        claudeDesktopWatcher?.Dispose();
        claudeCodeWatcher = claudeDesktopWatcher = null;
    }
}
