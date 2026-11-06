using System.Reflection;
using Slx.Client.Core;
using Slx.Client.Service;
using Xunit;

namespace Slx.Client.Tests;

public sealed class CoreRecoveryPolicyTests
{
    private static readonly CoreRecoveryResources HealthyFront = new(true, true, true, true);

    [Theory]
    [InlineData("HEALTH_PROBE_FAILED")]
    [InlineData("VPN_PROBE_FAILED")]
    [InlineData("CORE_EXITED")]
    [InlineData("CORE_START_FAILED")]
    [InlineData("CORE_STOP_FAILED")]
    [InlineData("CORE_CONTAINMENT_FAILED")]
    [InlineData("RUNTIME_HASH_MISMATCH")]
    public void VpnFailuresDoNotAuthorizeDestructionOfAHealthyFront(string code)
    {
        var policy = new CoreRecoveryPolicy();
        for (int retry = 0; retry < 5; retry++)
            Assert.Equal(TunnelLifecycleScope.VpnCore, policy.Choose(HealthyFront, CoreRecoveryPolicy.ReasonFor(code)).Scope);
        Assert.Null(policy.RequiredFullReason);
    }

    [Theory]
    [InlineData(false, true, true, true, TunnelLifecycleReason.FrontUnavailable)]
    [InlineData(true, false, true, true, TunnelLifecycleReason.AdapterUnavailable)]
    [InlineData(true, true, false, true, TunnelLifecycleReason.LocalLinkUnavailable)]
    [InlineData(true, true, true, false, TunnelLifecycleReason.LocalLinkUnavailable)]
    public void LostLocalResourcesStillRequireFullRecovery(bool front, bool adapter, bool probe, bool link,
        TunnelLifecycleReason reason)
    {
        var decision = new CoreRecoveryPolicy().Choose(new(front, adapter, probe, link), TunnelLifecycleReason.CoreExited);
        Assert.Equal(new(TunnelLifecycleScope.FullTunnel, reason), decision);
    }

    [Theory]
    [InlineData(TunnelLifecycleReason.ProtectionBaselineInvalid)]
    [InlineData(TunnelLifecycleReason.TrafficRouteFailed)]
    public void InvalidBaselineStaysFullUntilACompletedFullLaunchRestoresIt(TunnelLifecycleReason reason)
    {
        var policy = new CoreRecoveryPolicy();
        policy.RequireFull(reason);
        Assert.Equal(new(TunnelLifecycleScope.FullTunnel, reason), policy.Choose(HealthyFront, TunnelLifecycleReason.Requested));
        Assert.Equal(new(TunnelLifecycleScope.FullTunnel, reason), policy.Choose(HealthyFront, TunnelLifecycleReason.HealthProbeFailed));
        policy.FullBaselineRestored();
        Assert.Equal(TunnelLifecycleScope.VpnCore, policy.Choose(HealthyFront, TunnelLifecycleReason.HealthProbeFailed).Scope);
    }

    [Fact]
    public void AnotherRouteFailureCannotForgetAnInvalidNativeProtectionBaseline()
    {
        var policy = new CoreRecoveryPolicy();
        policy.RequireFull(TunnelLifecycleReason.ProtectionBaselineInvalid);
        policy.RequireFull(TunnelLifecycleReason.TrafficRouteFailed);
        Assert.Equal(TunnelLifecycleReason.ProtectionBaselineInvalid, policy.RequiredFullReason);
        Assert.Throws<ArgumentOutOfRangeException>(() => policy.RequireFull(TunnelLifecycleReason.CoreExited));
    }

    [Fact]
    public void ProductionPeriodicRouteFailureRequiresAFullBaselineAndWithdrawsSuccess()
    {
        var engine = new TunnelEngine();
        Field("state").SetValue(engine, new TunnelStatus(TunnelPhase.Connected, DateTimeOffset.UtcNow,
            ProbePassed: true, ProtectionActive: true));
        Field("protectionActive").SetValue(engine, true);
        var failure = (ClientFault)typeof(TunnelEngine).GetMethod("TrafficRouteFailure", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(engine, [CancellationToken.None])!;
        Assert.Equal(TrafficRouteProbe.FailureCode, failure.Code);
        Assert.Equal(TunnelPhase.Reconnecting, engine.Status.Phase);
        Assert.False(engine.Status.ProbePassed); Assert.True(engine.Status.ProtectionActive);
        var policy = (CoreRecoveryPolicy)Field("coreRecovery").GetValue(engine)!;
        Assert.Equal(new(TunnelLifecycleScope.FullTunnel, TunnelLifecycleReason.TrafficRouteFailed),
            policy.Choose(HealthyFront, TunnelLifecycleReason.CoreExited));
    }

    private static FieldInfo Field(string name) => typeof(TunnelEngine).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
}
