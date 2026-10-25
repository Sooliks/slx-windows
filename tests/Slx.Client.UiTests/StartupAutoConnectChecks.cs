using System.Net.NetworkInformation;
using System.Text.Json;
using Slx.Client.Core;
using Slx.Client.Desktop.Localization;
using Slx.Client.Desktop.ViewModels;
using Xunit;
namespace Slx.Client.UiTests;
internal static class StartupAutoConnectChecks
{
    public static void Run()
    {
        Assert.True(new ClientPreferences().AutoConnectOnStartup);
        Assert.False(new ClientPreferences().AutoStart);
        Assert.True(JsonSerializer.Deserialize<ClientPreferences>("{}")!.AutoConnectOnStartup);
        CheckLaunchChoices(); CheckRefreshThenConnect(); CheckDelayedServiceAndCachedFailure(); CheckDelayedNetwork();
        CheckExistingIntent(); CheckStartupCleanupWait(); CheckUnknownIsNotRetried(); CheckManualOffCancels(); CheckExplicitChangesCancel();
    }
    private static MainViewModel Model(bool tray = true, bool autoStart = true, bool autoConnect = true)
    {
        var model = new MainViewModel(false, UiFixtures.Subscription,
            UiFixtures.Preferences(RoutingMode.SelectedApplications) with { AutoStart = autoStart, AutoConnectOnStartup = autoConnect });
        model.StartupNetworkAvailableOverride = () => true;
        model.ArmStartupConnection(tray);
        return model;
    }
    private static PipeReply Reply(TunnelPhase phase = TunnelPhase.Disconnected, bool protection = false, string? code = null) =>
        new(true, "OK", new(phase, DateTimeOffset.UtcNow, Code: code, ProbePassed: phase == TunnelPhase.Connected, ProtectionActive: protection));
    private static void Refresh(MainViewModel model)
    {
        model.StartupFetchOverride = (_, _) => Task.FromResult(UiFixtures.Subscription.Snapshot);
        model.RefreshOnStartupAsync().GetAwaiter().GetResult();
    }
    private static void Poll(MainViewModel model) => model.PollAsync().GetAwaiter().GetResult();
    private static void CheckLaunchChoices()
    {
        foreach (var choice in new[] { (false, true, true), (true, false, true), (true, true, false) })
        {
            using var model = Model(choice.Item1, choice.Item2, choice.Item3);
            var requests = new List<PipeRequest>();
            model.SendOverride = (request, _) => { requests.Add(request); return Task.FromResult(Reply()); };
            Refresh(model); Poll(model); Poll(model);
            Assert.All(requests, request => Assert.Equal("status", request.Action));
        }
        // Uninitialized render/fixture models cannot acquire an automatic startup intent.
        using var render = new MainViewModel(false, UiFixtures.Subscription, new(AutoStart: true));
        render.SendOverride = (request, _) => { Assert.Equal("status", request.Action); return Task.FromResult(Reply()); };
        Refresh(render); Poll(render);
    }
    private static void CheckRefreshThenConnect()
    {
        using var model = Model();
        var requests = new List<PipeRequest>();
        var pending = new TaskCompletionSource<SubscriptionSnapshot>();
        model.SendOverride = (request, _) => { requests.Add(request); return Task.FromResult(Reply(request.Action == "status" ? TunnelPhase.Disconnected : TunnelPhase.Connected)); };
        model.StartupFetchOverride = (_, _) => pending.Task;
        var refresh = model.RefreshOnStartupAsync(); Poll(model);
        Assert.True(model.IsSubscriptionRefreshing); Assert.Equal("status", Assert.Single(requests).Action);
        var latest = UiFixtures.Primary with { Port = 9444 };
        pending.SetResult(UiFixtures.Subscription.Snapshot with { Profiles = [latest, UiFixtures.Alternative] }); Sta.Pump();
        Assert.True(refresh.IsCompletedSuccessfully); Assert.False(model.IsSubscriptionRefreshing);
        var sent = Assert.Single(requests, request => request.Action != "status");
        Assert.Equal("connect-if-idle", sent.Action); Assert.Equal(9444, sent.Plan!.Profile.Port);
        Poll(model); Assert.Single(requests, request => request.Action != "status");
    }
    private static void CheckDelayedServiceAndCachedFailure()
    {
        using var model = Model();
        int polls = 0; var requests = new List<PipeRequest>();
        model.StartupFetchOverride = (_, _) => Task.FromException<SubscriptionSnapshot>(new ClientFault("NETWORK_UNAVAILABLE"));
        model.SendOverride = (request, _) =>
        {
            requests.Add(request);
            if (request.Action == "status" && ++polls < 3) return Task.FromException<PipeReply>(new ClientFault("SERVICE_UNAVAILABLE"));
            return Task.FromResult(Reply());
        };
        model.RefreshOnStartupAsync().GetAwaiter().GetResult(); Poll(model); Poll(model);
        Assert.All(requests, request => Assert.Equal("status", request.Action));
        Poll(model); Poll(model);
        var sent = Assert.Single(requests, request => request.Action != "status");
        Assert.Equal("connect-if-idle", sent.Action); Assert.Equal(UiFixtures.Primary.Id, sent.Plan!.Profile.Id);
        Assert.Equal(2, model.Profiles.Count);
    }
    private static void CheckExistingIntent()
    {
        foreach (var state in new[] { (TunnelPhase.Connected, false), (TunnelPhase.Connecting, false), (TunnelPhase.Reconnecting, false), (TunnelPhase.Checking, true),
            (TunnelPhase.Disconnected, true), (TunnelPhase.Faulted, true), (TunnelPhase.Faulted, false), (TunnelPhase.Stopping, false) })
        {
            using var model = Model();
            var pending = new TaskCompletionSource<SubscriptionSnapshot>();
            model.StartupFetchOverride = (_, _) => pending.Task;
            model.SendOverride = (request, _) => { Assert.Equal("status", request.Action); return Task.FromResult(Reply(state.Item1, state.Item2, state.Item2 ? "SERVICE_STARTUP_CLEANUP_PENDING" : "CORE_FAILED")); };
            var refresh = model.RefreshOnStartupAsync(); Poll(model);
            model.SendOverride = (request, _) => { Assert.Equal("status", request.Action); return Task.FromResult(Reply()); };
            pending.SetResult(UiFixtures.Subscription.Snapshot); Sta.Pump();
            Assert.True(refresh.IsCompletedSuccessfully); Poll(model);
        }
    }
    private static void CheckStartupCleanupWait()
    {
        foreach (var manualOff in new[] { false, true })
        {
            using var model = Model(); var requests = new List<PipeRequest>(); bool ready = false;
            model.SendOverride = (request, _) =>
            {
                requests.Add(request);
                if (request.Action == "disconnect") return Task.FromResult(new PipeReply(false, "SERVICE_TIMEOUT", Reply().Status));
                return Task.FromResult(ready ? Reply() : Reply(TunnelPhase.Faulted, code: "SERVICE_STARTUP_CLEANUP_PENDING"));
            };
            Refresh(model); Poll(model); Poll(model);
            Assert.All(requests, request => Assert.Equal("status", request.Action));
            Assert.False(model.CanConnect);
            Assert.Equal(Text.Catalog["ServiceStartupHelp"], model.StatusDetail);
            if (manualOff) Assert.False(model.ExitAsync().GetAwaiter().GetResult());
            ready = true; Poll(model); Poll(model);
            Assert.Equal(manualOff ? "disconnect" : "connect-if-idle", Assert.Single(requests, request => request.Action != "status").Action);
        }
    }
    private static void CheckDelayedNetwork()
    {
        foreach (var queryFailure in new[] { false, true })
        {
            using var model = Model(); var requests = new List<PipeRequest>(); bool available = false;
            model.StartupNetworkAvailableOverride = () => !available && queryFailure ? throw new NetworkInformationException() : available;
            model.SendOverride = (request, _) => { requests.Add(request); return Task.FromResult(Reply()); };
            Refresh(model); Poll(model); Poll(model);
            Assert.All(requests, request => Assert.Equal("status", request.Action));
            Assert.False(model.IsBusy);
            if (queryFailure) Assert.True(model.ShowMessage);
            available = true; Poll(model); Poll(model);
            Assert.Equal("connect-if-idle", Assert.Single(requests, request => request.Action != "status").Action);
        }
    }
    private static void CheckUnknownIsNotRetried()
    {
        using var model = Model(); var requests = new List<PipeRequest>();
        model.SendOverride = (request, _) =>
        {
            requests.Add(request);
            return request.Action == "status" ? Task.FromResult(Reply()) : Task.FromException<PipeReply>(new ClientFault("SERVICE_TIMEOUT"));
        };
        Refresh(model); Poll(model); Assert.Equal("SERVICE_TIMEOUT", model.LastServiceCode);
        Poll(model); Poll(model);
        Assert.Equal("connect-if-idle", Assert.Single(requests, request => request.Action != "status").Action);
        Assert.False(model.IsBusy); Assert.False(model.IsConnected);
    }
    private static void CheckManualOffCancels()
    {
        using var model = Model(); var requests = new List<PipeRequest>();
        var pending = new TaskCompletionSource<SubscriptionSnapshot>();
        model.StartupFetchOverride = (_, _) => pending.Task;
        model.SendOverride = (request, _) =>
        {
            requests.Add(request); return Task.FromResult(Reply(request.Action == "connect" ? TunnelPhase.Connected : TunnelPhase.Disconnected));
        };
        var refresh = model.RefreshOnStartupAsync(); Poll(model);
        model.ToggleConnectionCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        model.ToggleConnectionCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        pending.SetResult(UiFixtures.Subscription.Snapshot); Sta.Pump();
        Assert.True(refresh.IsCompletedSuccessfully); Poll(model);
        Assert.Equal(new[] { "connect", "disconnect" }, requests.Where(request => request.Action != "status").Select(request => request.Action));
        Assert.False(model.IsTunnelActive);
    }
    private static void CheckExplicitChangesCancel()
    {
        foreach (var choice in new[] { "opt-out", "import", "remove" })
        {
            using var model = Model(); var requests = new List<PipeRequest>();
            model.SendOverride = (request, _) => { requests.Add(request); return Task.FromResult(Reply()); };
            Refresh(model);
            if (choice == "opt-out") { model.AutoConnectOnStartup = false; model.AutoConnectOnStartup = true; }
            else if (choice == "import") model.OpenImportCommand.Execute(null);
            else model.AskRemoveCommand.Execute(null);
            Poll(model); Assert.All(requests, request => Assert.Equal("status", request.Action));
        }
    }
}
