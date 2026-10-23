using Slx.Client.Core;
using Slx.Client.Desktop.Localization;
using Slx.Client.Desktop.ViewModels;
using Xunit;
namespace Slx.Client.UiTests;
internal static class RoutingDeferralChecks
{
    // An app edit made while the service status is stale was previously dropped and stayed "applying" forever.
    // Nothing had been sent, so the edit is submitted once after the next authoritative status; never replayed.
    public static void Run()
    {
        var requests = new List<PipeRequest>(); var statusFails = false;
        using var model = new MainViewModel(false, UiFixtures.Subscription, UiFixtures.Preferences(RoutingMode.SelectedApplications));
        UiFixtures.AllowSyntheticApplications(model);
        model.SendOverride = (request, _) =>
        {
            requests.Add(request);
            if (request.Action == "status" && statusFails) return Task.FromException<PipeReply>(new ClientFault("SERVICE_TIMEOUT"));
            return Task.FromResult(new PipeReply(true, "OK", new(TunnelPhase.Connected, DateTimeOffset.UtcNow,
                Mode: RoutingMode.SelectedApplications, ProbePassed: true, ProtectionActive: true)));
        };
        model.PollAsync().GetAwaiter().GetResult(); Assert.True(model.IsConnected);
        statusFails = true; model.PollAsync().GetAwaiter().GetResult(); Assert.False(model.IsConnected); Assert.True(model.IsTunnelActive);
        requests.Clear();
        model.Applications[0].Enabled = false;
        AutoRoutingChecks.WaitQuiet();
        Assert.DoesNotContain(requests, request => request.Action != "status");
        Assert.True(model.RulesPending);
        Assert.Equal(Text.Catalog["RulesUnconfirmed"], model.RoutingUpdateText);
        statusFails = false; model.PollAsync().GetAwaiter().GetResult();
        AutoRoutingChecks.Settle(() => requests.Any(request => request.Action != "status") && !model.IsBusy);
        var sent = Assert.Single(requests, request => request.Action != "status");
        Assert.Equal("update-applications", sent.Action);
        Assert.Contains(sent.Plan!.Applications, app => app.Id == model.Applications[0].Id && !app.Enabled);
        Assert.False(model.RulesPending);
        model.PollAsync().GetAwaiter().GetResult(); AutoRoutingChecks.WaitQuiet();
        Assert.Single(requests, request => request.Action != "status");
    }
}
