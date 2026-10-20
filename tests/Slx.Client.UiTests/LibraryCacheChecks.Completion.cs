using Slx.Client.Core;
using Slx.Client.Desktop.Localization;
using Slx.Client.Windows;
using Xunit;

namespace Slx.Client.UiTests;

internal static partial class LibraryCacheChecks
{
    private static void CompleteScanPrunesAndPersistsOnlyFreshRows()
    {
        using var model = Model();
        var stale = App("Old removed app"); var fresh = App("Fresh app");
        var cached = Cached(stale);
        model.DiscoveryCacheLoadOverride = () => Task.FromResult<ApplicationDiscoverySnapshot?>(cached);
        var savedToDisk = Signal(); int scans = 0;
        var saves = new List<ApplicationDiscoverySnapshot>();
        model.DiscoveryCacheSaveOverride = snapshot => { saves.Add(snapshot); return savedToDisk.Task; };
        model.DiscoveryScanOverride = (_, publish, _) =>
        {
            scans++;
            foreach (var source in Enum.GetValues<DiscoverySource>())
                publish(new(source, source is DiscoverySource.Running or DiscoverySource.Installed ? [fresh] : [], true));
            return Task.CompletedTask;
        };
        model.DiscoverCommand.Execute(null);
        Assert.True(model.Discovering); Assert.Single(saves);
        Assert.Equal(fresh, Assert.Single(model.Discovered));
        Sta.Pump(); // Queued progressive callbacks must not restore old rows during an async cache save.
        Assert.True(model.Discovering);
        Assert.Equal(fresh, Assert.Single(model.Discovered));
        var saved = Assert.Single(saves);
        Assert.Equal(fresh, Assert.Single(saved.Applications));
        Assert.True(saved.CapturedAt > cached.CapturedAt);
        savedToDisk.SetResult(); Until(() => !model.Discovering);
        Assert.Equal(Text.Catalog["LibraryUpdated"], model.LibraryStatusText);
        Assert.Equal(saved.CapturedAt.ToLocalTime().ToString("g"), model.LibraryObservationText);
        Assert.Equal(1, scans); Assert.Empty(model.Applications);
        model.RefreshLibraryIfNeededAsync().GetAwaiter().GetResult();
        Assert.Equal(1, scans); // Automatic revisit reuses the recent successful scan.
        model.DiscoverCommand.Execute(null);
        Assert.Equal(2, scans); Assert.Equal(2, saves.Count); // Manual refresh bypasses the interval.
    }

    private static void MissingCachedApplicationCannotBecomeSelected()
    {
        using var model = Model();
        var missing = App("No longer installed");
        model.DiscoveryCacheLoadOverride = () => Task.FromResult<ApplicationDiscoverySnapshot?>(Cached(missing));
        model.DiscoveryScanOverride = (_, publish, _) =>
        {
            foreach (var source in Enum.GetValues<DiscoverySource>()) publish(new(source, [], false));
            return Task.CompletedTask;
        };
        model.WarmDiscoveryCacheAsync().GetAwaiter().GetResult();
        var validated = new List<AppRule>();
        var reply = new TaskCompletionSource<IReadOnlyList<AppRule>>(TaskCreationOptions.RunContinuationsAsynchronously);
        model.LibraryValidateOverride = (paths, _) => { validated.AddRange(paths); return reply.Task; };
        var entry = Assert.Single(model.FilteredLibrary);
        var add = model.AddLibraryCommand.ExecuteAsync(entry);
        Assert.False(add.IsCompleted); Assert.Empty(model.Applications);
        reply.SetResult([]); Until(() => add.IsCompleted);
        Assert.True(add.IsCompletedSuccessfully);
        Assert.Equal(missing, Assert.Single(validated));
        Assert.Empty(model.Applications); Assert.False(model.RulesPending);
        Assert.Equal(Text.Catalog["LibraryAppUnavailable"], model.Message);
        Until(() => !model.Discovering);
    }

    private static void EachSearchCanRevealMoreThanTheFirstPage()
    {
        using var model = Model();
        foreach (var app in Enumerable.Range(0, 26).Select(index => App("Paged app " + index.ToString("D2"))))
            model.Discovered.Add(app);
        Assert.Equal(12, model.FilteredLibrary.Count); Assert.True(model.HasMoreLibraryResults);
        model.ShowMoreApplicationsCommand.Execute(null);
        Assert.Equal(24, model.FilteredLibrary.Count); Assert.True(model.HasMoreLibraryResults);
        model.DiscoverySearch = "Paged app 0";
        Assert.Equal(10, model.FilteredLibrary.Count); Assert.False(model.HasMoreLibraryResults);
        model.DiscoverySearch = "Paged app";
        Assert.Equal(12, model.FilteredLibrary.Count); Assert.True(model.HasMoreLibraryResults);
        Assert.Empty(model.Applications);
    }
}
