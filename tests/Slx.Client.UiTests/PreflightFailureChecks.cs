using Slx.Client.Core;
using Slx.Client.Desktop.Localization;
using Slx.Client.Desktop.ViewModels;
using Xunit;

namespace Slx.Client.UiTests;

internal static class PreflightFailureChecks
{
    public static void Run()
    {
        foreach (var (code, key) in new[]
        {
            ("ENDPOINT_TIMEOUT", "FaultEndpointNetwork"), ("ENDPOINT_UNREACHABLE", "FaultEndpointNetwork"),
            ("ENDPOINT_NOT_PUBLIC", "FaultEndpointRejected"), ("CORE_MISSING", "FaultCore"),
            ("RUNTIME_HASH_MISMATCH", "FaultRuntimeValidation"), ("RUNTIME_UNSAFE_PATH", "FaultRuntimeValidation")
        }) FailedPreflightSurvivesStatusAndClearsAtTheNextAction(code, key);
        foreach (var code in new[] { "SERVICE_UNAVAILABLE", "SERVICE_TIMEOUT", "OPERATION_INTERRUPTED", "IPC_REQUEST_TIMEOUT" })
            RecoveredCommunicationFaultIsNotRetained(code);
        HealthyObservationClearsRetainedFailure();
    }

    private static void FailedPreflightSurvivesStatusAndClearsAtTheNextAction(string code, string key)
    {
        using var model = Model();
        var attempts = 0;
        model.SendOverride = (request, _) =>
        {
            if (request.Action == "status") return Task.FromResult(Off());
            Assert.Equal("connect", request.Action);
            if (++attempts == 1) return Task.FromResult(new PipeReply(false, code, Off().Status));
            Assert.Null(model.LastServiceCode); // The previous failure ends with the next explicit attempt.
            return Task.FromResult(Healthy());
        };
        model.PollAsync().GetAwaiter().GetResult();
        model.ToggleConnectionCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        Assert.Equal(code, model.LastServiceCode);
        for (var poll = 0; poll < 2; poll++)
        {
            model.PollAsync().GetAwaiter().GetResult();
            Assert.Equal(code, model.LastServiceCode);
            Assert.Equal(Text.Catalog[key], model.StatusDetail);
            Assert.False(model.IsConnected); Assert.False(model.IsTunnelActive);
            Assert.Equal(Text.Catalog["Connect"], model.ConnectionAction);
        }
        Assert.Contains(code, model.SafeDiagnostics);
        Assert.DoesNotContain(UiFixtures.Primary.Host, model.SafeDiagnostics);
        model.ToggleConnectionCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        Assert.Equal(2, attempts); Assert.True(model.IsConnected); Assert.Null(model.LastServiceCode);
    }

    private static void RecoveredCommunicationFaultIsNotRetained(string code)
    {
        using var model = Model();
        model.SendOverride = (request, _) => request.Action == "status" ? Task.FromResult(Off())
            : Task.FromResult(new PipeReply(false, code, Off().Status));
        model.PollAsync().GetAwaiter().GetResult();
        model.ToggleConnectionCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        Assert.Equal(code, model.LastServiceCode);
        model.PollAsync().GetAwaiter().GetResult();
        Assert.Null(model.LastServiceCode);
        Assert.Equal(Text.Catalog["Connect"], model.ConnectionAction);
    }

    private static void HealthyObservationClearsRetainedFailure()
    {
        using var model = Model();
        var observed = Off();
        model.SendOverride = (request, _) => Task.FromResult(request.Action == "status" ? observed
            : new PipeReply(false, "ENDPOINT_TIMEOUT", Off().Status));
        model.PollAsync().GetAwaiter().GetResult();
        model.ToggleConnectionCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        model.PollAsync().GetAwaiter().GetResult();
        Assert.Equal("ENDPOINT_TIMEOUT", model.LastServiceCode);
        observed = Healthy();
        model.PollAsync().GetAwaiter().GetResult();
        Assert.True(model.IsConnected); Assert.Null(model.LastServiceCode);
        observed = Off();
        model.PollAsync().GetAwaiter().GetResult();
        Assert.Null(model.LastServiceCode);
    }

    private static MainViewModel Model()
    {
        var model = new MainViewModel(false, UiFixtures.Subscription, UiFixtures.Preferences(RoutingMode.WholeComputer));
        UiFixtures.AllowSyntheticApplications(model);
        return model;
    }
    private static PipeReply Off() => new(true, "OK", new(TunnelPhase.Disconnected, DateTimeOffset.UtcNow));
    private static PipeReply Healthy() => new(true, "OK", new(TunnelPhase.Connected, DateTimeOffset.UtcNow,
        ProbePassed: true, ProtectionActive: true));
}
