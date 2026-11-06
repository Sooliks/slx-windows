using Slx.Client.Core;
using Slx.Client.Desktop.ViewModels;
using Xunit;

namespace Slx.Client.UiTests;

internal static class LifecycleDiagnosticsChecks
{
    public static void Run()
    {
        using var model = new MainViewModel(false, UiFixtures.Subscription,
            UiFixtures.Preferences(RoutingMode.SelectedApplications));
        var since = new DateTimeOffset(2026, 11, 6, 12, 0, 0, TimeSpan.Zero);
        TunnelLifecycleEvent[] events =
        [
            new(1, since, TunnelLifecycleOrigin.ConnectRequest, TunnelLifecycleScope.FullTunnel,
                TunnelLifecycleReason.Requested, TunnelLifecycleOutcome.Connected, 1, 1),
            new(2, since.AddMinutes(1), TunnelLifecycleOrigin.AutomaticRecovery, TunnelLifecycleScope.VpnCore,
                TunnelLifecycleReason.HealthProbeFailed, TunnelLifecycleOutcome.Failed, 1, 2, "VPN_PROBE_FAILED"),
            new(3, since.AddMinutes(2), TunnelLifecycleOrigin.AutomaticRecovery, TunnelLifecycleScope.FullTunnel,
                TunnelLifecycleReason.AdapterUnavailable, TunnelLifecycleOutcome.Connected, 2, 3)
        ];
        var observed = new TunnelStatus(TunnelPhase.Connected, since.AddMinutes(2), UiFixtures.Primary.Name,
            Mode: RoutingMode.SelectedApplications, ProbePassed: true, ProtectionActive: true,
            ServiceVersion: "0.1.0-alpha.28", Lifecycle: new(since, 2, 3, events));
        var requests = new List<string>();
        model.SendOverride = (request, _) =>
        {
            requests.Add(request.Action);
            return Task.FromResult(new PipeReply(true, "OK", observed));
        };
        model.PollAsync().GetAwaiter().GetResult();
        var text = model.SafeDiagnostics;
        Assert.Contains("Service version: 0.1.0-alpha.28", text);
        Assert.Contains($"Lifecycle since UTC: {since:O}", text);
        Assert.Contains("Lifecycle generations: tun=2, core=3", text);
        Assert.Contains("Lifecycle retained events: 3, limit=16", text);
        Assert.Contains("origin=ConnectRequest, scope=FullTunnel, reason=Requested", text);
        Assert.Contains("origin=AutomaticRecovery, scope=VpnCore, reason=HealthProbeFailed, outcome=Failed", text);
        Assert.Contains("reason=AdapterUnavailable, outcome=Connected, tun_generation=2, core_generation=3", text);
        Assert.Contains("code=VPN_PROBE_FAILED", text);
        Assert.Contains("request origin does not prove a manual click", text);
        Assert.Contains("generations count started processes, not uninterrupted traffic", text);
        AssertPrivateDataAbsent(text);

        // Formatting accepts only the fixed public codes, even if a malformed observation reaches the UI.
        observed = observed with { ServiceVersion = UiFixtures.Subscription.Url, Lifecycle = observed.Lifecycle! with
            { Events = [events[1] with { Code = UiFixtures.Subscription.Url + "\n" + UiFixtures.Primary.Secret,
                Reason = (TunnelLifecycleReason)999 }] } };
        model.PollAsync().GetAwaiter().GetResult();
        text = model.SafeDiagnostics;
        Assert.Contains("Service version: unknown", text);
        Assert.Contains("reason=Unknown", text);
        Assert.DoesNotContain("code=", text);
        AssertPrivateDataAbsent(text);

        // The copied report stays bounded even if a future sender supplies more history than supported.
        var oversized = Enumerable.Range(1, TunnelLifecycleObservation.MaximumEvents + 3)
            .Select(sequence => events[0] with { Sequence = sequence }).ToArray();
        observed = observed with { Lifecycle = observed.Lifecycle! with { Events = oversized } };
        model.PollAsync().GetAwaiter().GetResult();
        text = model.SafeDiagnostics;
        Assert.Contains("Lifecycle retained events: 16, limit=16", text);
        Assert.DoesNotContain("Lifecycle event 1:", text);
        Assert.DoesNotContain("Lifecycle event 3:", text);
        Assert.Contains("Lifecycle event 4:", text);
        Assert.Contains("Lifecycle event 19:", text);

        // A reply without the optional history replaces the previous report, rather than retaining stale events.
        observed = new(TunnelPhase.Disconnected, since.AddMinutes(3));
        model.PollAsync().GetAwaiter().GetResult();
        Assert.DoesNotContain("Lifecycle ", model.SafeDiagnostics);
        Assert.Contains("Service version: unknown", model.SafeDiagnostics);
        Assert.All(requests, action => Assert.Equal("status", action));
    }

    private static void AssertPrivateDataAbsent(string text)
    {
        Assert.DoesNotContain(UiFixtures.Primary.Name, text);
        Assert.DoesNotContain(UiFixtures.Primary.Host, text);
        Assert.DoesNotContain(UiFixtures.Primary.Secret, text);
        Assert.DoesNotContain(UiFixtures.Subscription.Url, text);
        Assert.DoesNotContain(@"C:\SLX UI Fixture", text);
    }
}
