using Slx.Client.Core;
using Slx.Client.Service;
using Xunit;
namespace Slx.Client.Tests;
/// <summary>Real cross-owner coordinator with injected persistence/control ports; no service, WFP or network.</summary>
public sealed partial class ApplicationUpdateTransactionTests
{
    [Fact]
    public async Task SuccessfulUpdateProtectsUnionAndPersistsDesiredBeforeOneAtomicCoreReplacement()
    {
        var fixture=new TransactionFixture();var transaction=fixture.Create();
        await transaction.StartAsync(default);
        Assert.Equal(new[]{"protect:Union","save:after","replace","save:after","protect:After"},fixture.Events);
        Assert.True(transaction.Attempted);Assert.True(transaction.Applied);Assert.True(transaction.Finished);
        Assert.Null(transaction.FailureCode);Assert.Equal(1,fixture.ReplaceCalls);Assert.Equal(0,fixture.ReadCalls);
        await transaction.ReconcileAsync();Assert.Equal(5,fixture.Events.Count);
    }
    [Fact]
    public async Task FailedInitialDurableSaveRestoresOldProtectionWithoutSendingAnyCoreMutation()
    {
        var fixture=new TransactionFixture{Save=_=>throw new IOException("Injected initial persistence failure")};
        var transaction=fixture.Create();
        await Assert.ThrowsAsync<IOException>(()=>transaction.StartAsync(default));
        Assert.Equal(new[]{"protect:Union","save:after","protect:Before"},fixture.Events);
        Assert.False(transaction.Attempted);Assert.False(transaction.Finished);
        Assert.Equal(0,fixture.ReplaceCalls+fixture.ReadCalls);
    }
    [Fact]
    public async Task UnionProtectionFailureCannotPublishIntentOrSendRouting()
    {
        var fixture=new TransactionFixture{Protect=_=>throw new ClientFault("TRAFFIC_PROTECTION_BEGIN_80320012")};
        var transaction=fixture.Create();
        await Assert.ThrowsAsync<ClientFault>(()=>transaction.StartAsync(default));
        Assert.Equal(new[]{"protect:Union"},fixture.Events);
        Assert.False(transaction.Attempted);Assert.Equal(0,fixture.ReplaceCalls+fixture.ReadCalls);
    }
    [Fact]
    public async Task CancellationBeforeUnionHasNoEffects()
    {
        var fixture=new TransactionFixture();var transaction=fixture.Create();using var cancellation=new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>transaction.StartAsync(cancellation.Token));
        Assert.Empty(fixture.Events);Assert.False(transaction.Attempted);
    }
    [Fact]
    public async Task CallerCancellationAfterSubmissionIsReconciledWithAnIndependentReadBudget()
    {
        using var cancellation=new CancellationTokenSource();var fixture=new TransactionFixture();
        fixture.Replace=(_,_)=>{cancellation.Cancel();return Task.FromException(new OperationCanceledException(cancellation.Token));};
        fixture.Read=token=>
        {
            Assert.True(token.CanBeCanceled);Assert.False(token.IsCancellationRequested);
            return Task.FromResult(XrayRouteRevision.For(fixture.After));
        };
        var transaction=fixture.Create();await transaction.StartAsync(cancellation.Token);
        Assert.True(transaction.Finished);Assert.True(transaction.Applied);
        Assert.Equal(1,fixture.ReplaceCalls);Assert.Equal(1,fixture.ReadCalls);
    }
    private sealed class TransactionFixture
    {
        public ConnectPlan Before{get;}=CoreFixtures.Plan() with{Profile=CoreFixtures.Profile() with{Host="1.1.1.1"},
            Applications=[CoreFixtures.App(@"C:\Apps\before.exe")]};
        public ConnectPlan After{get;}
        public List<string> Events{get;}=[];
        public int ReplaceCalls{get;private set;}
        public int ReadCalls{get;private set;}
        public Action<ApplicationProtectionStage>? Protect{get;set;}
        public Action<bool>? Save{get;set;}
        public Func<ConnectPlan,CancellationToken,Task>? Replace{get;set;}
        public Func<CancellationToken,Task<string>>? Read{get;set;}
        public TransactionFixture()=>After=Before with{Applications=[CoreFixtures.App(@"C:\Apps\after.exe")]};
        public ApplicationUpdateTransaction Create()=>new(Before,After,
            stage=>{Events.Add("protect:"+stage);Protect?.Invoke(stage);},
            after=>{Events.Add(after?"save:after":"save:before");Save?.Invoke(after);},
            (plan,token)=>
            {
                Events.Add("replace");ReplaceCalls++;Assert.Same(After,plan);
                return Replace?.Invoke(plan,token)??Task.CompletedTask;
            },
            token=>
            {
                Events.Add("read");ReadCalls++;
                return Read?.Invoke(token)??Task.FromResult(XrayRouteRevision.For(After));
            });
    }
}
