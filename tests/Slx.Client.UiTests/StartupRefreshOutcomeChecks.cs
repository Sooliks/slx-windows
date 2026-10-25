using Slx.Client.Core;
using Slx.Client.Desktop.Localization;
using Slx.Client.Desktop.ViewModels;
using Xunit;
namespace Slx.Client.UiTests;

internal static class StartupRefreshOutcomeChecks
{
    public static void Run()
    {
        OwnTimeoutRetainsAndLabelsCachedProfiles();
        ActiveTunnelSkipsReplacementAndLabelsCachedProfiles();
        ExplicitCancellationIsNotReportedAsRefreshFailure();
    }
    private static MainViewModel Model() => new(false, UiFixtures.Subscription with
    { Snapshot = UiFixtures.Subscription.Snapshot with { Profiles = [UiFixtures.Alternative] } });

    private static void OwnTimeoutRetainsAndLabelsCachedProfiles()
    {
        using var model = Model();
        model.StartupRefreshBudget = TimeSpan.FromMilliseconds(15);
        model.StartupFetchOverride = (_, token) =>
        {
            var pending = new TaskCompletionSource<SubscriptionSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
            token.Register(() => pending.TrySetCanceled(token)); return pending.Task;
        };
        var refresh = model.RefreshOnStartupAsync(); Until(() => refresh.IsCompleted);
        Assert.True(refresh.IsCompletedSuccessfully); Assert.Single(model.Profiles);
        Assert.Equal(UiFixtures.Alternative.Id, model.SelectedProfile?.Id);
        Assert.Equal(Text.Catalog["CachedProfiles"], model.SubscriptionRefreshHint);
    }

    private static void ActiveTunnelSkipsReplacementAndLabelsCachedProfiles()
    {
        using var model = Model();
        model.SendOverride = (_, _) => Task.FromResult(new PipeReply(true, "OK", new(TunnelPhase.Connected,
            DateTimeOffset.UtcNow, ProbePassed: true, ProtectionActive: true)));
        model.PollAsync().GetAwaiter().GetResult();
        model.StartupFetchOverride = (_, _) => Task.FromResult(UiFixtures.Subscription.Snapshot);
        model.RefreshOnStartupAsync().GetAwaiter().GetResult();
        Assert.Single(model.Profiles); Assert.True(model.IsConnected);
        Assert.Equal(Text.Catalog["CachedProfiles"], model.SubscriptionRefreshHint);
    }

    private static void ExplicitCancellationIsNotReportedAsRefreshFailure()
    {
        using var model = Model();
        var pending = new TaskCompletionSource<SubscriptionSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        model.StartupFetchOverride = (_, token) => { token.Register(() => pending.TrySetCanceled(token)); return pending.Task; };
        var refresh = model.RefreshOnStartupAsync();
        model.OpenImportCommand.Execute(null); Until(() => refresh.IsCompleted);
        Assert.True(refresh.IsCompletedSuccessfully); Assert.True(model.ImportVisible); Assert.Single(model.Profiles);
        Assert.Equal(Text.Catalog["ProfilesAutoRefresh"], model.SubscriptionRefreshHint);
    }
    private static void Until(Func<bool> completed)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        while (!completed() && clock.Elapsed < TimeSpan.FromSeconds(3)) { Sta.Pump(); Thread.Sleep(1); }
        Assert.True(completed());
    }
}
