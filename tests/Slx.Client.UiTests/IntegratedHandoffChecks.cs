using Slx.Client.Core;
using Slx.Client.Desktop.ViewModels;
using Slx.Client.Windows;
using Xunit;
namespace Slx.Client.UiTests;
internal static class IntegratedHandoffChecks
{
    private static readonly UpdateOffer Offer = new("0.1.0-alpha.26", UpdateSourcePolicy.ReleasePage("0.1.0-alpha.26"),
        UpdateSourcePolicy.InstallerName("0.1.0-alpha.26"),
        new("https://github.com/Sooliks/slx-windows/releases/download/v0.1.0-alpha.26/SLX-VPN-0.1.0-alpha.26-win-x64-setup.exe"), 1, new string('a', 64));
    public static void Run()
    {
        ActiveWithoutConsentDoesNotDisconnect();
        ExplicitConsentPreparesServiceBeforeHandoff();
        FailedHandoffReleasesPreparedMaintenance();
        UnknownPrepareNeverLaunchesHost();
        MismatchedServiceDoesNotDisconnect();
    }
    private static MainViewModel Model() => new(false, UiFixtures.Subscription, UiFixtures.Preferences(RoutingMode.WholeComputer));
    private static Task<UpdateApplyResult> Apply(MainViewModel model, bool resume) => model.ApplyIntegratedUpdateAsync(
        new(Offer, @"C:\SLX UI Fixture\NeverLaunched.exe"), resume, new Progress<UpdateApplyProgress>(), CancellationToken.None);
    private static PipeReply Reply(MainViewModel model, TunnelPhase phase, Guid? session = null) => new(true, "OK",
        new(phase, DateTimeOffset.UtcNow, ProbePassed: phase == TunnelPhase.Connected, ProtectionActive: phase == TunnelPhase.Connected,
            ServiceVersion: model.VersionLabel["SLX VPN · ".Length..],
            UpdateMaintenance: session is { } id ? new(id, DateTimeOffset.UtcNow.AddMinutes(5), false) : null));
    private static void ActiveWithoutConsentDoesNotDisconnect()
    {
        using var model = Model(); var actions = new List<string>();
        model.SendOverride = (request, _) => { actions.Add(request.Action); return Task.FromResult(Reply(model, TunnelPhase.Connected)); };
        Assert.Equal("UPDATE_CONNECTION_ACTIVE", Fault(Apply(model, false)));
        Assert.Equal(new[] { "status" }, actions); Assert.True(model.IsConnected);
    }
    private static void ExplicitConsentPreparesServiceBeforeHandoff()
    {
        using var model = Model(); var actions = new List<string>(); Guid prepared = Guid.Empty;
        model.SendOverride = (request, _) =>
        {
            actions.Add(request.Action);
            if (request.Action == "prepare-update") prepared = request.UpdateSessionId!.Value;
            return Task.FromResult(Reply(model, request.Action == "status" ? TunnelPhase.Connected : TunnelPhase.Disconnected,
                request.Action == "prepare-update" ? prepared : null));
        };
        model.UpdateHandoffOverride = (_, id, resume, fingerprint, _) =>
        {
            Assert.Equal(prepared, id); Assert.True(resume); Assert.Equal(64, fingerprint!.Length);
            Assert.False(model.CanConnect); actions.Add("host-ready"); return Task.CompletedTask;
        };
        Assert.Equal(UpdateApplyResult.Restarting, Complete(Apply(model, true)));
        Assert.Equal(new[] { "status", "disconnect", "prepare-update", "host-ready" }, actions);
        Assert.False(model.CanConnect);
    }
    private static void FailedHandoffReleasesPreparedMaintenance()
    {
        using var model = Model(); var actions = new List<string>();
        model.SendOverride = (request, _) =>
        {
            actions.Add(request.Action);
            return Task.FromResult(Reply(model, TunnelPhase.Disconnected,
                request.Action == "prepare-update" ? request.UpdateSessionId : null));
        };
        model.UpdateHandoffOverride = (_, _, _, _, _) => throw new ClientFault("UPDATE_HANDOFF_FAILED");
        Assert.Equal("UPDATE_HANDOFF_FAILED", Fault(Apply(model, false)));
        Assert.Equal(new[] { "status", "prepare-update", "release-update" }, actions); Assert.True(model.CanConnect);
    }
    private static void UnknownPrepareNeverLaunchesHost()
    {
        using var model = Model(); int launches = 0;
        model.SendOverride = (request, _) => request.Action == "status" ? Task.FromResult(Reply(model, TunnelPhase.Disconnected))
            : Task.FromException<PipeReply>(new System.IO.IOException());
        model.UpdateHandoffOverride = (_, _, _, _, _) => { launches++; return Task.CompletedTask; };
        Assert.Equal("UPDATE_HANDOFF_FAILED", Fault(Apply(model, false))); Assert.Equal(0, launches);
        Assert.Contains("Service observation: unknown", model.SafeDiagnostics);
    }
    private static void MismatchedServiceDoesNotDisconnect()
    {
        using var model = Model(); int requests = 0;
        model.SendOverride = (_, _) => { requests++; var value = Reply(model, TunnelPhase.Connected); return Task.FromResult(value with { Status = value.Status with { ServiceVersion = "0.1.0-alpha.1" } }); };
        Assert.Equal("UPDATE_SERVICE_VERSION", Fault(Apply(model, true))); Assert.Equal(1, requests);
    }
    private static string Fault(Task task)
    { AutoRoutingChecks.Settle(() => task.IsCompleted); return Assert.Throws<ClientFault>(() => task.GetAwaiter().GetResult()).Code; }
    private static T Complete<T>(Task<T> task)
    { AutoRoutingChecks.Settle(() => task.IsCompleted); return task.GetAwaiter().GetResult(); }
}
