using Slx.Client.Core;
using Slx.Client.Service;
using Slx.Client.Windows;
using Xunit;
namespace Slx.Client.Tests;
public sealed partial class ApplicationUpdateTransactionTests
{
    [Fact]
    public async Task LostAcknowledgementWithDesiredRevisionFinalizesWithoutReplayingTheCoreMutation()
    {
        var fixture=new TransactionFixture{Replace=(_,_)=>Task.FromException(new ClientFault("ROUTING_TIMEOUT"))};
        var transaction=fixture.Create();await transaction.StartAsync(default);
        Assert.True(transaction.Applied);Assert.True(transaction.Finished);
        Assert.Equal(new[]{"protect:Union","save:after","replace","read","save:after","protect:After"},fixture.Events);
        await transaction.ReconcileAsync();Assert.Equal(1,fixture.ReplaceCalls);Assert.Equal(1,fixture.ReadCalls);
    }
    [Fact]
    public async Task LostAcknowledgementWithOldRevisionRestoresOldIntentAndProtectionWithoutCoreRetry()
    {
        var fixture=new TransactionFixture{Replace=(_,_)=>Task.FromException(new ClientFault("ROUTING_UNAVAILABLE"))};
        fixture.Read=_=>Task.FromResult(XrayRouteRevision.For(fixture.Before));
        var transaction=fixture.Create();await transaction.StartAsync(default);
        Assert.True(transaction.Finished);Assert.False(transaction.Applied);Assert.Equal("ROUTING_UPDATE_FAILED",transaction.FailureCode);
        Assert.Equal(new[]{"protect:Union","save:after","replace","read","save:before","protect:Before"},fixture.Events);
        await transaction.ReconcileAsync();Assert.Equal(1,fixture.ReplaceCalls);Assert.Equal(1,fixture.ReadCalls);
    }
    [Fact]
    public async Task UnknownRevisionRetainsDurableDesiredAndUnionUntilAReadOnlyReconciliationResolvesIt()
    {
        var fixture=new TransactionFixture{Replace=(_,_)=>Task.FromException(new ClientFault("ROUTING_TIMEOUT"))};
        var revision="unexpected-generation";fixture.Read=_=>Task.FromResult(revision);
        var transaction=fixture.Create();await transaction.StartAsync(default);
        Assert.False(transaction.Finished);Assert.Equal(new[]{"protect:Union","save:after","replace","read"},fixture.Events);
        await transaction.ReconcileAsync();Assert.Equal(1,fixture.ReplaceCalls);Assert.Equal(2,fixture.ReadCalls);
        revision=XrayRouteRevision.For(fixture.After);await transaction.ReconcileAsync();
        Assert.True(transaction.Finished);Assert.True(transaction.Applied);
        Assert.Equal(1,fixture.ReplaceCalls);Assert.Equal(3,fixture.ReadCalls);
        Assert.DoesNotContain("save:before",fixture.Events);Assert.DoesNotContain("protect:Before",fixture.Events);
    }
    [Fact]
    public async Task FinalPersistenceFailureRetainsUnionAndRetriesOnlyFinalization()
    {
        var fixture=new TransactionFixture();int saves=0;
        fixture.Save=after=>{Assert.True(after);if(++saves==2)throw new UnauthorizedAccessException("Injected final save failure");};
        var transaction=fixture.Create();await transaction.StartAsync(default);
        Assert.False(transaction.Finished);Assert.DoesNotContain("protect:After",fixture.Events);
        await transaction.ReconcileAsync();Assert.True(transaction.Finished);Assert.True(transaction.Applied);
        Assert.Equal(1,fixture.ReplaceCalls);Assert.Equal(0,fixture.ReadCalls);Assert.Equal(3,saves);
    }
    [Fact]
    public async Task FailedFinalNativeCommitKeepsUnionAndInvalidatesFurtherNativeDeltasWithoutReplayingCore()
    {
        var fixture=new TransactionFixture();
        var before=TrafficApplicationPolicy.FromIdentities([[1]]);var after=TrafficApplicationPolicy.FromIdentities([[2]]);
        var state=new TrafficApplicationState(before);int nativeCommits=0;
        fixture.Protect=stage=>state.Apply(stage switch
        {
            ApplicationProtectionStage.Before=>before,ApplicationProtectionStage.After=>after,_=>before.Union(after)
        },_=>
        {
            nativeCommits++;
            if(stage==ApplicationProtectionStage.After)throw new ClientFault("TRAFFIC_PROTECTION_COMMIT_80320010");
        });
        var transaction=fixture.Create();await transaction.StartAsync(default);
        Assert.False(transaction.Finished);Assert.Equal(2,state.Filters.Count);
        Assert.Throws<ClientFault>(()=>state.Current);
        await transaction.ReconcileAsync();
        Assert.False(transaction.Finished);Assert.Equal("TRAFFIC_PROTECTION_APPLICATION_BASELINE",transaction.FailureCode);
        Assert.Equal(2,nativeCommits);Assert.Equal(1,fixture.ReplaceCalls);Assert.Equal(0,fixture.ReadCalls);
    }
}
