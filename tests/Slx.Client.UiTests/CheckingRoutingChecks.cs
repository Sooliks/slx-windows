using Slx.Client.Core;
using Slx.Client.Desktop.ViewModels;
using Xunit;
namespace Slx.Client.UiTests;
internal static class CheckingRoutingChecks
{
    public static void Run()
    {
        foreach (var resume in new[] { true, false })
        {
            using var model = new MainViewModel(false, UiFixtures.Subscription,
                UiFixtures.Preferences(RoutingMode.SelectedApplications));
            UiFixtures.AllowSyntheticApplications(model);
            var observed = new TunnelStatus(TunnelPhase.Checking, DateTimeOffset.UtcNow,
                ConnectedAt: DateTimeOffset.UtcNow.AddMinutes(-10), ProtectionActive: true);
            var requests = new List<PipeRequest>();
            model.SendOverride = (request, _) =>
            {
                requests.Add(request);
                return Task.FromResult(new PipeReply(true, "OK", observed));
            };
            model.PollAsync().GetAwaiter().GetResult(); requests.Clear();
            var app = model.Applications[0];
            app.Enabled = false;
            AutoRoutingChecks.WaitQuiet();
            Assert.True(model.RulesPending); Assert.Empty(requests);
            // A later choice replaces the queued version, and repeated Checking polls submit nothing.
            app.Enabled = true; app.Enabled = false;
            model.PollAsync().GetAwaiter().GetResult();
            AutoRoutingChecks.WaitQuiet();
            Assert.All(requests, request => Assert.Equal("status", request.Action));
            observed = resume ? observed with { Phase = TunnelPhase.Connected, ProbePassed = true }
                : new(TunnelPhase.Disconnected, DateTimeOffset.UtcNow);
            model.PollAsync().GetAwaiter().GetResult();
            if (resume)
            {
                AutoRoutingChecks.Settle(() => requests.Any(request => request.Action != "status") && !model.IsBusy);
                var sent = Assert.Single(requests, request => request.Action != "status");
                Assert.Equal("update-applications", sent.Action);
                Assert.Contains(sent.Plan!.Applications, rule => rule.Id == app.Id && !rule.Enabled);
                Assert.False(model.RulesPending);
            }
            model.PollAsync().GetAwaiter().GetResult(); AutoRoutingChecks.WaitQuiet();
            Assert.Equal(resume ? 1 : 0, requests.Count(request => request.Action != "status"));
            Assert.DoesNotContain(requests, request => request.Action is "connect" or "reconnect");
        }
    }
}
