using Slx.Client.Core;
using Slx.Client.Desktop.ViewModels;
using Xunit;
namespace Slx.Client.UiTests;
internal static class HealthDiagnosticsChecks
{
    public static void Run()
    {
        using var model = new MainViewModel(false, UiFixtures.Subscription,
            UiFixtures.Preferences(RoutingMode.SelectedApplications));
        var time = DateTimeOffset.UtcNow;
        var observed = new TunnelStatus(TunnelPhase.Connected, time, UiFixtures.Primary.Name,
            ConnectedAt: time.AddMinutes(-10), ProbePassed: true, ProtectionActive: true,
            HealthCheck: new(10, 10, time,
                new(ProbeTarget.Gstatic, ProbeOutcome.Timeout, 5000),
                new(ProbeTarget.Gstatic, ProbeOutcome.HttpRejected, 81, 503),
                new(ProbeTarget.Slx, ProbeOutcome.Success, 42, 200)));
        model.SendOverride = (_, _) => Task.FromResult(new PipeReply(true, "OK", observed));
        model.PollAsync().GetAwaiter().GetResult();
        var text = model.SafeDiagnostics;
        Assert.Contains("Health incidents: 10, retained: 10", text);
        Assert.Contains("Health primary: Gstatic, Timeout, 5000 ms", text);
        Assert.Contains("Health repeat: Gstatic, HttpRejected, 81 ms, http_status=503", text);
        Assert.Contains("Health alternate: Slx, Success, 42 ms, http_status=200", text);
        Assert.Contains($"Connected UTC: {observed.ConnectedAt:O}", text);
        Assert.DoesNotContain("Recovery:", text);
        Assert.DoesNotContain(UiFixtures.Primary.Name, text);
        Assert.DoesNotContain(UiFixtures.Primary.Host, text);
        Assert.DoesNotContain(UiFixtures.Primary.Secret, text);
        Assert.DoesNotContain("synthetic-ui-fixture", text);
        Assert.DoesNotContain(@"C:\SLX UI Fixture", text);
        // A later partial observation replaces all prior attempt output, including optional HTTP status.
        observed = observed with { Phase = TunnelPhase.Checking, ProbePassed = false,
            HealthCheck = new(11, 10, time.AddSeconds(30), new(ProbeTarget.Gstatic, ProbeOutcome.Tls, 50), null, null) };
        model.PollAsync().GetAwaiter().GetResult();
        text = model.SafeDiagnostics;
        Assert.Contains("Phase: Checking", text);
        Assert.Contains("Health primary: Gstatic, Tls, 50 ms", text);
        Assert.DoesNotContain("Health repeat:", text);
        Assert.DoesNotContain("Health alternate:", text);
        Assert.DoesNotContain("http_status=", text);
        observed = observed with { HealthCheck = observed.HealthCheck! with
            { Alternate = new(ProbeTarget.Slx, ProbeOutcome.Connection, 22) } };
        model.PollAsync().GetAwaiter().GetResult();
        Assert.Contains("Health alternate: Slx, Connection, 22 ms", model.SafeDiagnostics);
        Assert.DoesNotContain("Health repeat:", model.SafeDiagnostics);
        observed = new(TunnelPhase.Disconnected, time.AddMinutes(1));
        model.PollAsync().GetAwaiter().GetResult();
        Assert.DoesNotContain("Health ", model.SafeDiagnostics);
        Assert.DoesNotContain("Connected UTC:", model.SafeDiagnostics);
    }
}
