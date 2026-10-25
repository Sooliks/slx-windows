using Slx.Client.Core;
using Slx.Client.Desktop.Localization;
using Slx.Client.Desktop.ViewModels;
using Xunit;
namespace Slx.Client.UiTests;
internal static class SubscriptionActivityChecks
{
    public static void Run()
    {
        RefreshDoesNotInventATunnelOperation();
        ExplicitOffRemainsAvailableAndCancelsTheRefresh();
    }
    private static void RefreshDoesNotInventATunnelOperation()
    {
        using var model = Model(TunnelPhase.Disconnected);
        var pending = new TaskCompletionSource<SubscriptionSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        var actions = new List<string>();
        model.SendOverride = (request, _) => { actions.Add(request.Action); return Task.FromResult(Reply(TunnelPhase.Disconnected)); };
        model.SubscriptionFetchOverride = (_, token) => pending.Task.WaitAsync(token);
        var refreshing = model.RefreshCommand.ExecuteAsync(null);
        Assert.True(model.IsBusy); Assert.False(model.CanConnect);
        Assert.Equal(Text.Catalog["Connect"], model.ConnectionAction);
        Assert.Contains("UI operation: none", model.SafeDiagnostics);
        Assert.Contains("Phase: Disconnected", model.SafeDiagnostics);
        Complete(model.PollAsync());
        pending.SetResult(UiFixtures.Subscription.Snapshot with { Title = "Refreshed fixture" });
        Complete(refreshing);
        Assert.False(model.IsBusy); Assert.True(model.CanConnect);
        Assert.Equal("Refreshed fixture", model.SubscriptionTitle);
        Assert.Equal(new[] { "status" }, actions);
    }
    private static void ExplicitOffRemainsAvailableAndCancelsTheRefresh()
    {
        using var model = Model(TunnelPhase.Connected);
        var pending = new TaskCompletionSource<SubscriptionSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        var actions = new List<string>(); CancellationToken fetchToken = default;
        model.SendOverride = (request, _) => { actions.Add(request.Action); return Task.FromResult(Reply(TunnelPhase.Disconnected)); };
        model.SubscriptionFetchOverride = (_, token) => { fetchToken = token; return pending.Task.WaitAsync(token); };
        var original = model.SubscriptionTitle;
        var refreshing = model.RefreshCommand.ExecuteAsync(null);
        Assert.True(model.IsBusy); Assert.True(model.CanConnect);
        Assert.Equal(Text.Catalog["Disconnect"], model.ConnectionAction);
        Complete(model.ToggleConnectionCommand.ExecuteAsync(null)); Complete(refreshing);
        Assert.True(fetchToken.IsCancellationRequested);
        pending.SetResult(UiFixtures.Subscription.Snapshot with { Title = "Must not replace" }); Sta.Pump();
        Assert.Equal(original, model.SubscriptionTitle);
        Assert.False(model.IsBusy); Assert.False(model.IsConnected); Assert.False(model.IsTunnelActive);
        Assert.Equal(new[] { "disconnect" }, actions);
        Assert.Null(model.LastServiceCode);
    }
    private static MainViewModel Model(TunnelPhase phase)
    {
        var model = new MainViewModel(false, UiFixtures.Subscription, UiFixtures.Preferences(RoutingMode.WholeComputer));
        model.SendOverride = (_, _) => Task.FromResult(Reply(phase));
        Complete(model.PollAsync()); return model;
    }
    private static PipeReply Reply(TunnelPhase phase) => new(true, "OK", new(phase, DateTimeOffset.UtcNow,
        ProbePassed: phase == TunnelPhase.Connected, ProtectionActive: phase == TunnelPhase.Connected));
    private static void Complete(Task task)
    { AutoRoutingChecks.Settle(() => task.IsCompleted); task.GetAwaiter().GetResult(); }
}
