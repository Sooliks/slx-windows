using Slx.Client.Core;
using Slx.Client.Desktop.Localization;
using Slx.Client.Desktop.ViewModels;
using Xunit;

namespace Slx.Client.UiTests;

internal static class ConnectionRecoveryChecks
{
    public static void Run()
    {
        UnknownFirstConnectCanStillDisconnect();
        NoSubscriptionCanStillRequestOffWhenServiceStateIsUnknown();
        BusyConnectCanBeCancelledWithExplicitOff();
        StoppingNeverOffersANewConnect();
        RolledBackFailureRemainsVisible();
        PreflightFailureChecks.Run();
    }

    private static void UnknownFirstConnectCanStillDisconnect()
    {
        var requests = new List<string>();
        using var model = Model();
        model.SendOverride = (request, _) =>
        {
            requests.Add(request.Action);
            return request.Action == "connect"
                ? Task.FromException<PipeReply>(new ClientFault("SERVICE_UNAVAILABLE"))
                : Task.FromResult(Off());
        };
        model.PollAsync().GetAwaiter().GetResult();
        model.ToggleConnectionCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        Assert.False(model.IsBusy); Assert.False(model.IsConnected); Assert.False(model.IsTunnelActive);
        Assert.True(model.CanConnect); Assert.False(model.CanReconnect);
        Assert.Equal(Text.Catalog["Disconnect"], model.ConnectionAction);
        model.ToggleConnectionCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        Assert.Equal(new[] { "status", "connect", "disconnect" }, requests);
        Assert.Equal(Text.Catalog["Connect"], model.ConnectionAction);
        Assert.Null(model.LastServiceCode);
    }

    private static void NoSubscriptionCanStillRequestOffWhenServiceStateIsUnknown()
    {
        using var model = new MainViewModel(false);
        model.SendOverride = (request, _) => request.Action == "status"
            ? Task.FromException<PipeReply>(new ClientFault("SERVICE_UNAVAILABLE")) : Task.FromResult(Off());
        Assert.True(model.ShowImportAction);
        model.PollAsync().GetAwaiter().GetResult();
        Assert.True(model.CanConnect); Assert.True(model.ShowConnectionAction); Assert.False(model.ShowImportAction);
        Assert.Equal(Text.Catalog["Disconnect"], model.ConnectionAction);
        model.ToggleConnectionCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        Assert.False(model.CanConnect); Assert.False(model.ShowConnectionAction); Assert.True(model.ShowImportAction);
    }

    private static void BusyConnectCanBeCancelledWithExplicitOff()
    {
        var requests = new List<string>();
        using var model = Model();
        model.SendOverride = async (request, cancellationToken) =>
        {
            requests.Add(request.Action);
            if (request.Action == "connect") await Task.Delay(Timeout.Infinite, cancellationToken);
            return Off();
        };
        model.PollAsync().GetAwaiter().GetResult();
        var connecting = model.ToggleConnectionCommand.ExecuteAsync(null);
        AutoRoutingChecks.Settle(() => requests.Contains("connect"));
        Assert.True(model.IsBusy); Assert.True(model.CanConnect);
        Assert.Equal(Text.Catalog["Disconnect"], model.ConnectionAction);
        var disconnecting = model.ToggleConnectionCommand.ExecuteAsync(null);
        AutoRoutingChecks.Settle(() => connecting.IsCompleted && disconnecting.IsCompleted);
        connecting.GetAwaiter().GetResult(); disconnecting.GetAwaiter().GetResult();
        Assert.Equal(new[] { "status", "connect", "disconnect" }, requests);
        Assert.False(model.IsBusy); Assert.False(model.IsTunnelActive); Assert.False(model.IsConnected);
    }

    private static void StoppingNeverOffersANewConnect()
    {
        using var model = Model();
        model.SendOverride = (_, _) => Task.FromResult(new PipeReply(true, "OK",
            new(TunnelPhase.Stopping, DateTimeOffset.UtcNow)));
        model.PollAsync().GetAwaiter().GetResult();
        Assert.True(model.IsTunnelActive);
        Assert.Equal(Text.Catalog["Disconnect"], model.ConnectionAction);
    }

    private static void RolledBackFailureRemainsVisible()
    {
        using var model = Model();
        var failed = new TunnelStatus(TunnelPhase.Disconnected, DateTimeOffset.UtcNow, Code: "VPN_PROBE_FAILED");
        model.SendOverride = (_, _) => Task.FromResult(new PipeReply(true, "OK", failed));
        model.PollAsync().GetAwaiter().GetResult();
        model.PollAsync().GetAwaiter().GetResult();
        Assert.Equal("VPN_PROBE_FAILED", model.LastServiceCode);
        Assert.Equal(Text.Catalog["FaultVpnProbe"], model.StatusDetail);
        Assert.False(model.IsTunnelActive); Assert.False(model.IsConnected);
        Assert.Equal(Text.Catalog["Connect"], model.ConnectionAction);
        Assert.Contains("VPN_PROBE_FAILED", model.SafeDiagnostics);
    }

    private static MainViewModel Model()
    {
        var model = new MainViewModel(false, UiFixtures.Subscription, UiFixtures.Preferences(RoutingMode.WholeComputer));
        UiFixtures.AllowSyntheticApplications(model);
        return model;
    }
    private static PipeReply Off() => new(true, "OK", new(TunnelPhase.Disconnected, DateTimeOffset.UtcNow));
}
