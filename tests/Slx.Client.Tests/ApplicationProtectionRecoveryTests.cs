using System.Reflection;
using Slx.Client.Core;
using Slx.Client.Service;
using Xunit;
namespace Slx.Client.Tests;
public sealed class ApplicationProtectionRecoveryTests
{
    [Theory]
    [InlineData("union", false, 0)]
    [InlineData("rollback", false, 0)]
    [InlineData("after", true, 1)]
    [InlineData("before", false, 1)]
    public async Task FailedNativeCommitSchedulesProtectedRecoveryOfTheAuthoritativePlan(string failure, bool useAfter, int writes)
    {
        var before = CoreFixtures.Plan();
        var after = before with { Applications = [CoreFixtures.App(@"C:\Apps\replacement.exe")] };
        var pinnedBefore = before with { Profile = before.Profile with { Host = "1.1.1.1" } };
        var pinnedAfter = after with { Profile = pinnedBefore.Profile };
        int replacements = 0, reads = 0;
        var transaction = new ApplicationUpdateTransaction(before, after, stage =>
        {
            if (failure == "union" && stage == ApplicationProtectionStage.Union
                || (failure is "rollback" or "before") && stage == ApplicationProtectionStage.Before
                || failure == "after" && stage == ApplicationProtectionStage.After)
                throw new ClientFault("TRAFFIC_PROTECTION_COMMIT_80320010");
        }, _ => { if (failure == "rollback") throw new IOException("Injected durable save failure"); },
        (_, _) =>
        {
            replacements++;
            return failure == "before" ? Task.FromException(new ClientFault("ROUTING_UPDATE_FAILED")) : Task.CompletedTask;
        }, _ => { reads++; return Task.FromResult(XrayRouteRevision.For(before)); });
        if (failure is "union" or "rollback") await Assert.ThrowsAsync<ClientFault>(() => transaction.StartAsync(default));
        else await transaction.StartAsync(default);
        Assert.True(transaction.ProtectionRecoveryRequired); Assert.False(transaction.Finished);
        Assert.Equal(useAfter, transaction.RecoveryUsesAfter); Assert.Equal(writes, replacements);
        int initialReads = reads;
        await transaction.ReconcileAsync();
        Assert.Equal(initialReads, reads); Assert.Equal(writes, replacements); // Never repeat native delta/core mutation.

        var engine = new TunnelEngine();
        var connected = new TunnelStatus(TunnelPhase.Connected, DateTimeOffset.UtcNow, ProbePassed: true,
            ProtectionActive: true, ApplicationRevision: XrayRouteRevision.For(before));
        Field("state").SetValue(engine, connected); Field("protectionActive").SetValue(engine, true);
        Assert.True(engine.RecoverApplicationProtection(transaction, before, after, pinnedBefore, pinnedAfter));
        Assert.Equal(TunnelPhase.Reconnecting, engine.Status.Phase);
        Assert.True(engine.Status.ProtectionActive); Assert.False(engine.Status.ProbePassed);
        Assert.Null(engine.Status.ApplicationRevision);
        Assert.Equal("TRAFFIC_PROTECTION_COMMIT_80320010", engine.Status.Code);
        Assert.Same(useAfter ? after : before, Field("currentPlan").GetValue(engine));
        Assert.Same(useAfter ? pinnedAfter : pinnedBefore, Field("pinnedPlan").GetValue(engine));
    }
    private static FieldInfo Field(string name) => typeof(TunnelEngine).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
}
