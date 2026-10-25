using Slx.Client.Core;
using Slx.Client.Desktop.ViewModels;
using Xunit;
namespace Slx.Client.UiTests;
internal static class StatusCompletionChecks
{
    public static void Run()
    {
        AnIntermediateReadRequiresAFreshObservationAfterTheKnownConnectReply();
        foreach (var phase in new[] { TunnelPhase.Checking, TunnelPhase.Reconnecting })
            foreach (var failedReply in new[] { false, true }) ANewerStateCannotBeReplacedByALateAppReply(phase, failedReply);
        FailedFreshReadDoesNotReplayTheKnownMutation();
        ExplicitOffCanSupersedeThePendingRead();
    }
    private static void AnIntermediateReadRequiresAFreshObservationAfterTheKnownConnectReply()
    {
        using var model = Model(RoutingMode.WholeComputer);
        var connect = Pending(); var fresh = Pending(); int reads = 0, mutations = 0;
        model.SendOverride = (request, _) => request.Action == "status" ? ++reads switch
        { 1 => Task.FromResult(Reply(TunnelPhase.Disconnected)), 2 => Task.FromResult(Reply(TunnelPhase.Connecting)), _ => fresh.Task }
            : Mutation();
        Task<PipeReply> Mutation() { mutations++; return connect.Task; }
        Complete(model.PollAsync());
        var changing = model.ToggleConnectionCommand.ExecuteAsync(null);
        AutoRoutingChecks.Settle(() => mutations == 1);
        Complete(model.PollAsync());
        connect.SetResult(Reply(TunnelPhase.Connected));
        AutoRoutingChecks.Settle(() => reads == 3);
        Assert.False(model.IsConnected); Assert.True(model.IsBusy);
        Assert.Contains("Service observation: unknown/stale", model.SafeDiagnostics);
        Assert.Contains("UI operation: none", model.SafeDiagnostics);
        Complete(model.PollAsync()); Assert.Equal(3, reads);
        fresh.SetResult(Reply(TunnelPhase.Connected)); Complete(changing);
        Assert.True(model.IsConnected); Assert.False(model.IsBusy); Assert.Equal(1, mutations);
    }
    private static void ANewerStateCannotBeReplacedByALateAppReply(TunnelPhase phase, bool failedReply)
    {
        using var model = Model(RoutingMode.SelectedApplications);
        var update = Pending(); int mutations = 0, reads = 0;
        var observed = Reply(TunnelPhase.Connected);
        model.SendOverride = (request, _) =>
        {
            if (request.Action == "status") { reads++; return Task.FromResult(observed); }
            Assert.Equal("update-applications", request.Action); mutations++; return update.Task;
        };
        Complete(model.PollAsync());
        model.Applications[0].Enabled = false;
        AutoRoutingChecks.Settle(() => mutations == 1);
        observed = Reply(phase);
        Complete(model.PollAsync()); Assert.False(model.IsConnected);
        update.SetResult(failedReply ? new(false, "ROUTING_UPDATE_UNCONFIRMED", Reply(TunnelPhase.Connected).Status)
            : Reply(TunnelPhase.Connected));
        AutoRoutingChecks.Settle(() => !model.IsBusy);
        Assert.False(model.IsConnected); Assert.True(model.CanConnect);
        Assert.Contains("Phase: " + phase, model.SafeDiagnostics);
        Assert.Contains("Service observation: current", model.SafeDiagnostics);
        Assert.Equal(3, reads); Assert.Equal(1, mutations);
        Assert.Equal(failedReply, model.RulesPending);
    }
    private static void FailedFreshReadDoesNotReplayTheKnownMutation()
    {
        using var model = Model(RoutingMode.WholeComputer);
        var connect = Pending(); var fresh = Pending(); int reads = 0, mutations = 0;
        model.SendOverride = (request, _) => request.Action == "status" ? ++reads switch
        { 1 => Task.FromResult(Reply(TunnelPhase.Disconnected)), 2 => Task.FromResult(Reply(TunnelPhase.Connecting)), _ => fresh.Task }
            : Mutation();
        Task<PipeReply> Mutation() { mutations++; return connect.Task; }
        Complete(model.PollAsync());
        var changing = model.ToggleConnectionCommand.ExecuteAsync(null);
        AutoRoutingChecks.Settle(() => mutations == 1); Complete(model.PollAsync());
        connect.SetResult(Reply(TunnelPhase.Connected)); AutoRoutingChecks.Settle(() => reads == 3);
        fresh.SetException(new ClientFault("SERVICE_UNAVAILABLE")); Complete(changing);
        Assert.False(model.IsConnected); Assert.False(model.IsBusy); Assert.True(model.CanConnect);
        Assert.Equal("SERVICE_UNAVAILABLE", model.LastServiceCode);
        Assert.Contains("Service observation: unknown/stale", model.SafeDiagnostics);
        AutoRoutingChecks.WaitQuiet(); Assert.Equal(1, mutations);
    }
    private static void ExplicitOffCanSupersedeThePendingRead()
    {
        using var model = Model(RoutingMode.WholeComputer);
        var connect = Pending(); var fresh = Pending(); int reads = 0;
        var mutations = new List<string>();
        model.SendOverride = (request, _) =>
        {
            if (request.Action == "status") return ++reads switch
            { 1 => Task.FromResult(Reply(TunnelPhase.Disconnected)), 2 => Task.FromResult(Reply(TunnelPhase.Connecting)), _ => fresh.Task };
            mutations.Add(request.Action);
            return request.Action == "connect" ? connect.Task : Task.FromResult(Reply(TunnelPhase.Disconnected));
        };
        Complete(model.PollAsync());
        var changing = model.ToggleConnectionCommand.ExecuteAsync(null);
        AutoRoutingChecks.Settle(() => mutations.Count == 1); Complete(model.PollAsync());
        connect.SetResult(Reply(TunnelPhase.Connected)); AutoRoutingChecks.Settle(() => reads == 3);
        Complete(model.ToggleConnectionCommand.ExecuteAsync(null)); Complete(changing);
        fresh.SetResult(Reply(TunnelPhase.Connected)); Sta.Pump();
        Assert.False(model.IsConnected); Assert.False(model.IsBusy); Assert.False(model.IsTunnelActive);
        Assert.Contains("Phase: Disconnected", model.SafeDiagnostics);
        Assert.Equal(new[] { "connect", "disconnect" }, mutations);
    }
    private static MainViewModel Model(RoutingMode mode)
    {
        var model = new MainViewModel(false, UiFixtures.Subscription, UiFixtures.Preferences(mode));
        UiFixtures.AllowSyntheticApplications(model); return model;
    }
    private static TaskCompletionSource<PipeReply> Pending() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static PipeReply Reply(TunnelPhase phase) => new(true, "OK", new(phase, DateTimeOffset.UtcNow,
        ProbePassed: phase == TunnelPhase.Connected, ProtectionActive: phase != TunnelPhase.Disconnected));
    private static void Complete(Task task)
    { AutoRoutingChecks.Settle(() => task.IsCompleted); task.GetAwaiter().GetResult(); }
}
