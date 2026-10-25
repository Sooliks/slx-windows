using Slx.Client.Core;
using Slx.Client.Desktop.ViewModels;
using Xunit;
namespace Slx.Client.UiTests;
internal static class StatusObservationChecks
{
    public static void Run()
    {
        ConnectRemainsObservableWhileItsReplyIsBlocked();
        PendingReadsAreCoalescedAndFailuresRemainUnknown();
        ReadStartedBeforeMutationCannotOverwriteItsReply();
        foreach (var fail in new[] { false, true }) ReadDuringMutationCannotOverwriteItsReply(fail);
        ReadCannotEraseALaterUncertainMutation();
        StatusCompletionChecks.Run();
    }
    private static void ConnectRemainsObservableWhileItsReplyIsBlocked()
    {
        using var model = Model();
        var connectReply = Pending();
        var actions = new List<string>();
        var transition = DateTimeOffset.UtcNow.AddDays(-1);
        var observed = Reply(TunnelPhase.Disconnected, transition);
        model.SendOverride = (request, _) =>
        {
            actions.Add(request.Action);
            return request.Action == "status" ? Task.FromResult(observed) : connectReply.Task;
        };
        Complete(model.PollAsync());
        Assert.Contains($"Observed UTC: {transition:O}", model.SafeDiagnostics);
        Assert.DoesNotContain($"Service read UTC: {transition:O}", model.SafeDiagnostics);
        var connecting = model.ToggleConnectionCommand.ExecuteAsync(null);
        AutoRoutingChecks.Settle(() => actions.Contains("connect"));
        Assert.True(model.IsBusy);
        Assert.Contains("Service observation: unknown/stale", model.SafeDiagnostics);
        Assert.Contains("UI operation: connect, pending", model.SafeDiagnostics);
        Assert.Contains("UI operation started UTC:", model.SafeDiagnostics);
        observed = Reply(TunnelPhase.Connecting);
        Complete(model.PollAsync());
        Assert.True(model.IsBusy);
        Assert.Contains("Service observation: current", model.SafeDiagnostics);
        Assert.Contains("Phase: Connecting", model.SafeDiagnostics);
        observed = Reply(TunnelPhase.Connected);
        connectReply.SetResult(observed); Complete(connecting);
        Assert.True(model.IsConnected); Assert.False(model.IsBusy);
        Assert.Contains("UI operation: none", model.SafeDiagnostics);
        Assert.DoesNotContain("UI operation started UTC:", model.SafeDiagnostics);
        Assert.Equal(new[] { "status", "connect", "status", "status" }, actions);
    }
    private static void PendingReadsAreCoalescedAndFailuresRemainUnknown()
    {
        using var model = Model();
        var connectReply = Pending(); var statusReply = Pending();
        var actions = new List<string>(); var blockStatus = false;
        model.SendOverride = (request, token) =>
        {
            Assert.True(token.CanBeCanceled);
            actions.Add(request.Action);
            return request.Action != "status" ? connectReply.Task : blockStatus ? statusReply.Task : Task.FromResult(Reply(TunnelPhase.Disconnected));
        };
        Complete(model.PollAsync());
        var connecting = model.ToggleConnectionCommand.ExecuteAsync(null);
        AutoRoutingChecks.Settle(() => actions.Contains("connect"));
        blockStatus = true;
        var reading = model.PollAsync();
        Complete(model.PollAsync()); Complete(model.PollAsync());
        Assert.Equal(2, actions.Count(x => x == "status"));
        statusReply.SetException(new ClientFault("SERVICE_UNAVAILABLE")); Complete(reading);
        Assert.True(model.IsBusy);
        Assert.Contains("Service observation: unknown/stale", model.SafeDiagnostics);
        Assert.Contains("UI operation: connect, pending", model.SafeDiagnostics);
        Assert.Equal("SERVICE_UNAVAILABLE", model.LastServiceCode);
        connectReply.SetException(new ClientFault("SERVICE_TIMEOUT")); Complete(connecting);
        Assert.False(model.IsBusy); Assert.False(model.IsConnected);
        Assert.Equal("SERVICE_TIMEOUT", model.LastServiceCode);
        Assert.Single(actions, x => x == "connect");
    }
    private static void ReadStartedBeforeMutationCannotOverwriteItsReply()
    {
        using var model = Model();
        var statusReply = Pending(); var blockStatus = false;
        model.SendOverride = (request, _) => request.Action == "status"
            ? blockStatus ? statusReply.Task : Task.FromResult(Reply(TunnelPhase.Disconnected))
            : Task.FromResult(Reply(TunnelPhase.Connected));
        Complete(model.PollAsync()); blockStatus = true;
        var reading = model.PollAsync();
        Complete(model.ToggleConnectionCommand.ExecuteAsync(null));
        Assert.True(model.IsConnected);
        var accepted = model.SafeDiagnostics;
        statusReply.SetResult(Reply(TunnelPhase.Disconnected, DateTimeOffset.UtcNow.AddDays(1))); Complete(reading);
        Assert.True(model.IsConnected);
        Assert.Equal(ReadLine(accepted), ReadLine(model.SafeDiagnostics));
    }
    private static void ReadDuringMutationCannotOverwriteItsReply(bool fail)
    {
        using var model = Model();
        var connectReply = Pending(); var statusReply = Pending();
        var connectStarted = false;
        model.SendOverride = (request, _) =>
        {
            if (request.Action == "status") return connectStarted ? statusReply.Task : Task.FromResult(Reply(TunnelPhase.Disconnected));
            connectStarted = true; return connectReply.Task;
        };
        Complete(model.PollAsync());
        var connecting = model.ToggleConnectionCommand.ExecuteAsync(null);
        AutoRoutingChecks.Settle(() => connectStarted);
        var reading = model.PollAsync();
        connectReply.SetResult(Reply(TunnelPhase.Connected)); Complete(connecting);
        var accepted = model.SafeDiagnostics;
        if (fail) statusReply.SetException(new ClientFault("SERVICE_UNAVAILABLE"));
        else statusReply.SetResult(Reply(TunnelPhase.Connecting, DateTimeOffset.UtcNow.AddDays(1)));
        Complete(reading);
        Assert.True(model.IsConnected); Assert.Null(model.LastServiceCode);
        Assert.Contains("Service observation: current", model.SafeDiagnostics);
        Assert.Equal(ReadLine(accepted), ReadLine(model.SafeDiagnostics));
    }
    private static void ReadCannotEraseALaterUncertainMutation()
    {
        using var model = Model();
        var connectReply = Pending(); var statusReply = Pending();
        var connectStarted = false;
        model.SendOverride = (request, _) =>
        {
            if (request.Action == "status") return connectStarted ? statusReply.Task : Task.FromResult(Reply(TunnelPhase.Disconnected));
            connectStarted = true; return connectReply.Task;
        };
        Complete(model.PollAsync());
        var connecting = model.ToggleConnectionCommand.ExecuteAsync(null);
        AutoRoutingChecks.Settle(() => connectStarted);
        var reading = model.PollAsync();
        connectReply.SetException(new ClientFault("SERVICE_TIMEOUT")); Complete(connecting);
        statusReply.SetResult(Reply(TunnelPhase.Disconnected)); Complete(reading);
        Assert.Contains("Service observation: unknown/stale", model.SafeDiagnostics);
        Assert.Equal("SERVICE_TIMEOUT", model.LastServiceCode);
        Assert.False(model.IsConnected);
    }
    private static MainViewModel Model()
    {
        var model = new MainViewModel(false, UiFixtures.Subscription, UiFixtures.Preferences(RoutingMode.WholeComputer));
        UiFixtures.AllowSyntheticApplications(model);
        return model;
    }
    private static TaskCompletionSource<PipeReply> Pending() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static PipeReply Reply(TunnelPhase phase, DateTimeOffset? at = null) => new(true, "OK",
        new(phase, at ?? DateTimeOffset.UtcNow, ProbePassed: phase == TunnelPhase.Connected,
            ProtectionActive: phase is TunnelPhase.Connecting or TunnelPhase.Connected));
    private static string ReadLine(string text) => text.Split('\n').Single(x => x.StartsWith("Service read UTC:", StringComparison.Ordinal));
    private static void Complete(Task task)
    {
        AutoRoutingChecks.Settle(() => task.IsCompleted);
        task.GetAwaiter().GetResult();
    }
}
