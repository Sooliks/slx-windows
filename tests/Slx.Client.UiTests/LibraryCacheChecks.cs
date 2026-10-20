using System.Diagnostics;
using Slx.Client.Core;
using Slx.Client.Desktop.Localization;
using Slx.Client.Desktop.ViewModels;
using Slx.Client.Windows;
using Xunit;

namespace Slx.Client.UiTests;

internal static partial class LibraryCacheChecks
{
    public static void Run()
    {
        CachedAndFastRowsAppearDuringOneRefresh();
        PartialResultCannotEvictAnyFullBaselineRow();
        CompleteScanPrunesAndPersistsOnlyFreshRows();
        MissingCachedApplicationCannotBecomeSelected();
        EachSearchCanRevealMoreThanTheFirstPage();
    }

    private static MainViewModel Model() => new(false)
    {
        CatalogEntries = [],
        CatalogFetchOverride = _ => Task.FromResult<IReadOnlyList<CatalogApplication>>([])
    };
    private static AppRule App(string name) => new(Guid.NewGuid(), name, @"C:\SLX Cache UI Fixture\" + name + ".exe");
    private static ApplicationDiscoverySnapshot Cached(params AppRule[] apps) => new(DateTimeOffset.UtcNow.AddDays(-1), apps);
    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static void Until(Func<bool> completed)
    {
        var watch = Stopwatch.StartNew();
        while (!completed() && watch.Elapsed < TimeSpan.FromSeconds(3)) Sta.Pump();
        Assert.True(completed());
    }
    private static void FinishPartial(Action<DiscoveryBatch> publish)
    {
        foreach (var source in Enum.GetValues<DiscoverySource>().Where(source => source != DiscoverySource.Running))
            publish(new(source, [], source != DiscoverySource.Installed));
    }

    private static void CachedAndFastRowsAppearDuringOneRefresh()
    {
        var selected = UiFixtures.Preferences(RoutingMode.SelectedApplications);
        using var model = new MainViewModel(false, UiFixtures.Subscription, selected) { CatalogEntries = [] };
        model.CatalogFetchOverride = _ => Task.FromResult<IReadOnlyList<CatalogApplication>>([]);
        var requests = new List<PipeRequest>();
        model.SendOverride = (request, _) =>
        {
            requests.Add(request);
            return Task.FromResult(new PipeReply(true, "OK", new(TunnelPhase.Connected, DateTimeOffset.UtcNow,
                Mode: RoutingMode.SelectedApplications, ProbePassed: true, ProtectionActive: true)));
        };
        model.PollAsync().GetAwaiter().GetResult(); requests.Clear();
        var cacheReady = new TaskCompletionSource<ApplicationDiscoverySnapshot?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = Signal(); var snapshot = Cached(App("Saved messenger"));
        int loads = 0, scans = 0, saves = 0;
        Action<DiscoveryBatch>? publish = null;
        model.DiscoveryCacheLoadOverride = () => { loads++; return cacheReady.Task; };
        model.DiscoveryCacheSaveOverride = _ => { saves++; return Task.CompletedTask; };
        model.DiscoveryScanOverride = (_, callback, _) => { scans++; publish = callback; return finish.Task; };
        var warm = model.WarmDiscoveryCacheAsync();
        model.DiscoverCommand.Execute(null);
        Assert.True(model.Discovering); Assert.Equal(0, scans); Assert.Equal(1, loads);
        cacheReady.SetResult(snapshot);
        Until(() => scans == 1);
        Assert.True(warm.IsCompletedSuccessfully);
        Assert.Contains(model.FilteredLibrary, row => row.Name == "Saved messenger");
        Assert.Equal(snapshot.CapturedAt.ToLocalTime().ToString("g"), model.LibraryObservationText);
        publish!(new(DiscoverySource.Running, [App("Fast running app")], true)); Sta.Pump();
        Assert.True(model.Discovering);
        Assert.Contains(model.FilteredLibrary, row => row.Name == "Fast running app");
        Assert.Contains(model.FilteredLibrary, row => row.Name == "Saved messenger");
        Assert.Equal(Text.Catalog["LibraryRefreshing"], model.LibraryStatusText);
        Assert.False(model.DiscoverCommand.CanExecute(null)); model.DiscoverCommand.Execute(null);
        Assert.Equal(1, scans);
        FinishPartial(publish); finish.SetResult(); Until(() => !model.Discovering);
        Assert.Equal(0, saves);
        Assert.Equal(Text.Catalog["LibraryRefreshPartial"], model.LibraryStatusText);
        Assert.Equal(snapshot.CapturedAt.ToLocalTime().ToString("g"), model.LibraryObservationText);
        Assert.Equal(selected.Applications, model.Applications.SelectMany(item => item.ToRules()).ToList());
        Assert.False(model.RulesPending); Assert.Empty(requests);
    }

    private static void PartialResultCannotEvictAnyFullBaselineRow()
    {
        using var model = Model();
        var snapshot = Cached(Enumerable.Range(0, 300).Select(index => App("M saved " + index.ToString("D3"))).ToArray());
        model.DiscoveryCacheLoadOverride = () => Task.FromResult<ApplicationDiscoverySnapshot?>(snapshot);
        var finish = Signal(); Action<DiscoveryBatch>? publish = null; int saves = 0;
        model.DiscoveryScanOverride = (_, callback, _) => { publish = callback; return finish.Task; };
        model.DiscoveryCacheSaveOverride = _ => { saves++; return Task.CompletedTask; };
        model.DiscoverCommand.Execute(null);
        var fresh = Enumerable.Range(0, 300).Select(index => App("A new " + index.ToString("D3"))).ToArray();
        publish!(new(DiscoverySource.Running, fresh, true)); Sta.Pump();
        Assert.Equal(600, model.Discovered.Count);
        Assert.All(snapshot.Applications, row => Assert.Contains(model.Discovered, actual => actual.Id == row.Id));
        Assert.All(fresh, row => Assert.Contains(model.Discovered, actual => actual.Id == row.Id));
        FinishPartial(publish); finish.SetResult(); Until(() => !model.Discovering);
        Assert.Equal(600, model.Discovered.Count);
        Assert.All(snapshot.Applications, row => Assert.Contains(model.Discovered, actual => actual.Id == row.Id));
        Assert.Equal(snapshot.CapturedAt.ToLocalTime().ToString("g"), model.LibraryObservationText);
        Assert.Equal(0, saves); Assert.Empty(model.Applications);
    }
}
