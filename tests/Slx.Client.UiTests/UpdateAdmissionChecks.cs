using Slx.Client.Core;
using Slx.Client.Desktop.ViewModels;
using Slx.Client.Windows;
using Xunit;
namespace Slx.Client.UiTests;
internal static class UpdateAdmissionChecks
{
    private static readonly UpdateOffer Offer = new("0.1.0-alpha.25", UpdateSourcePolicy.ReleasePage("0.1.0-alpha.25"),
        UpdateSourcePolicy.InstallerName("0.1.0-alpha.25"),
        new("https://github.com/Sooliks/slx-windows/releases/download/v0.1.0-alpha.25/SLX-VPN-0.1.0-alpha.25-win-x64-setup.exe"), 1, new string('a', 64));
    public static void Run()
    {
        foreach (var phase in new[] { TunnelPhase.Connected, TunnelPhase.Checking, TunnelPhase.Reconnecting, TunnelPhase.Disconnected })
            foreach (var failure in new[] { false, true }) NewerObservationWinsOverInstallAdmission(phase, failure);
        ADirectActiveReplyRemainsAConfirmedObservation();
    }
    private static void NewerObservationWinsOverInstallAdmission(TunnelPhase phase, bool failedRead)
    {
        using var model = Model(); var updates = model.Updates;
        var pending = new TaskCompletionSource<PipeReply>(TaskCreationOptions.RunContinuationsAsynchronously);
        int reads = 0, launches = 0;
        model.SendOverride = (_, _) => ++reads == 1 ? pending.Task : Task.FromResult(Reply(phase));
        updates.LaunchOverride = (_, _) => { launches++; return Task.FromResult(UpdateLaunchResult.Cancelled); };
        var installing = updates.ApplyCommand.ExecuteAsync(null);
        Assert.Equal(1, reads); Assert.False(model.CanConnect);
        Complete(model.PollAsync());
        if (failedRead) pending.SetException(new ClientFault("SERVICE_TIMEOUT"));
        else pending.SetResult(Reply(TunnelPhase.Disconnected));
        Complete(installing);
        Assert.Equal(phase == TunnelPhase.Disconnected && !failedRead ? 1 : 0, launches);
        Assert.Contains("Phase: " + phase, model.SafeDiagnostics);
        Assert.Contains("Service observation: current", model.SafeDiagnostics);
        Assert.Null(model.LastServiceCode);
        Assert.Equal(phase == TunnelPhase.Connected, model.IsConnected);
        Assert.True(model.CanConnect); // The lease always releases, including rejected/failed admission.
    }
    private static void ADirectActiveReplyRemainsAConfirmedObservation()
    {
        using var model = Model(); int launches = 0;
        model.SendOverride = (_, _) => Task.FromResult(Reply(TunnelPhase.Connected));
        model.Updates.LaunchOverride = (_, _) => { launches++; return Task.FromResult(UpdateLaunchResult.Started); };
        Complete(model.Updates.ApplyCommand.ExecuteAsync(null));
        Assert.Equal(0, launches); Assert.True(model.IsConnected);
        Assert.Contains("Service observation: current", model.SafeDiagnostics);
        Assert.Null(model.LastServiceCode); Assert.True(model.CanConnect);
    }
    private static MainViewModel Model()
    {
        var model = new MainViewModel(false, UiFixtures.Subscription, UiFixtures.Preferences(RoutingMode.WholeComputer));
        model.SendOverride = (_, _) => Task.FromResult(Reply(TunnelPhase.Disconnected));
        Complete(model.PollAsync());
        model.Updates.CheckOverride = (_, _) => Task.FromResult(new UpdateCheckResult(UpdateCheckState.Available, Offer));
        model.Updates.DownloadOverride = (offer, _, _) => Task.FromResult(new PreparedUpdate(offer, @"C:\SLX UI Fixture\NeverLaunched.exe"));
        Complete(model.Updates.CheckAsync(true)); Complete(model.Updates.ApplyCommand.ExecuteAsync(null));
        return model;
    }
    private static PipeReply Reply(TunnelPhase phase) => new(true, "OK", new(phase, DateTimeOffset.UtcNow,
        ProbePassed: phase == TunnelPhase.Connected, ProtectionActive: phase != TunnelPhase.Disconnected));
    private static void Complete(Task task)
    { AutoRoutingChecks.Settle(() => task.IsCompleted); task.GetAwaiter().GetResult(); }
}
