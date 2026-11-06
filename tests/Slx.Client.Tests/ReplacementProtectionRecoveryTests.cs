using System.Reflection;
using Slx.Client.Core;
using Slx.Client.Service;
using Xunit;

namespace Slx.Client.Tests;

public sealed class ReplacementProtectionRecoveryTests
{
    [Theory]
    [InlineData("TRAFFIC_PROTECTION_OPEN_000006BA")]
    [InlineData("TRAFFIC_PROTECTION_BEGIN_80320012")]
    [InlineData("TRAFFIC_PROTECTION_COMMIT_80320010")]
    public void FailedNativeReplacementWithdrawsSuccessAndRebuildsTheOldAuthoritativePlan(string code)
    {
        var (engine, plan, pinned, _) = Established();
        var failure = new ClientFault(code);
        Assert.Same(failure, Assert.Throws<ClientFault>(() =>
            engine.ApplyReplacementProtection(() => throw failure)));
        Assert.Same(plan, Field("currentPlan").GetValue(engine));
        Assert.Same(pinned, Field("pinnedPlan").GetValue(engine));
        Assert.Equal("owner", engine.OwnerSid);
        Assert.Equal(true, Field("desiredConnected").GetValue(engine));
        Assert.Equal(TunnelPhase.Reconnecting, engine.Status.Phase);
        Assert.True(engine.Status.ProtectionActive);
        Assert.False(engine.Status.ProbePassed);
        Assert.Null(engine.Status.ApplicationRevision);
        Assert.Equal(code, engine.Status.Code);
        var policy = (CoreRecoveryPolicy)Field("coreRecovery").GetValue(engine)!;
        Assert.Equal(new CoreRecoveryDecision(TunnelLifecycleScope.FullTunnel, TunnelLifecycleReason.ProtectionBaselineInvalid),
            policy.Choose(new(true, true, true, true), TunnelLifecycleReason.HealthProbeFailed));
    }

    [Theory]
    [InlineData("RUNTIME_HASH_MISMATCH")]
    [InlineData("SERVICE_IDENTITY_FAILED")]
    public void NonNativePreflightFailureCannotInvalidateTheEstablishedBaseline(string code)
    {
        var (engine, plan, pinned, status) = Established();
        Assert.Throws<ClientFault>(() => engine.ApplyReplacementProtection(() => throw new ClientFault(code)));
        Assert.Same(status, Field("state").GetValue(engine));
        Assert.Same(plan, Field("currentPlan").GetValue(engine));
        Assert.Same(pinned, Field("pinnedPlan").GetValue(engine));
        Assert.Null(((CoreRecoveryPolicy)Field("coreRecovery").GetValue(engine)!).RequiredFullReason);
    }

    [Fact]
    public async Task InitialOffAttemptKeepsItsExistingDefinitiveFailureRollback()
    {
        var engine = new TunnelEngine();
        int rollbacks = 0;
        var failure = new ClientFault("TRAFFIC_PROTECTION_COMMIT_80320010");
        Assert.Same(failure, await Assert.ThrowsAsync<ClientFault>(() => InitialConnectionAttempt.RunAsync(true,
            () => { engine.ApplyReplacementProtection(() => throw failure); return Task.CompletedTask; },
            code => { Assert.Equal(failure.Code, code); rollbacks++; return Task.CompletedTask; }, default)));
        Assert.Equal(1, rollbacks);
        Assert.Equal(TunnelPhase.Disconnected, engine.Status.Phase);
        Assert.False(engine.Status.ProtectionActive);
        Assert.Null(Field("currentPlan").GetValue(engine));
        Assert.Null(((CoreRecoveryPolicy)Field("coreRecovery").GetValue(engine)!).RequiredFullReason);
    }

    private static (TunnelEngine Engine, ConnectPlan Plan, ConnectPlan Pinned, TunnelStatus Status) Established()
    {
        var engine = new TunnelEngine();
        var plan = CoreFixtures.Plan();
        var pinned = plan with { Profile = plan.Profile with { Host = "1.1.1.1" } };
        var status = new TunnelStatus(TunnelPhase.Connected, DateTimeOffset.UtcNow,
            ProbePassed: true, ProtectionActive: true, ApplicationRevision: XrayRouteRevision.For(plan));
        Field("state").SetValue(engine, status);
        Field("currentPlan").SetValue(engine, plan);
        Field("pinnedPlan").SetValue(engine, pinned);
        Field("ownerSid").SetValue(engine, "owner");
        Field("desiredConnected").SetValue(engine, true);
        Field("protectionActive").SetValue(engine, true);
        return (engine, plan, pinned, status);
    }

    private static FieldInfo Field(string name) => typeof(TunnelEngine).GetField(name,
        BindingFlags.Instance | BindingFlags.NonPublic)!;
}
