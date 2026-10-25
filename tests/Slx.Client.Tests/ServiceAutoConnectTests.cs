using Slx.Client.Core;
using Slx.Client.Service;
using Xunit;
namespace Slx.Client.Tests;
/// <summary>Atomic dispatcher contracts only; no Windows startup registration, service or real VPN.</summary>
public sealed class ServiceAutoConnectTests
{
    [Fact]
    public async Task IdleAutoConnectStartsOnceAndAnotherStartupRequestDoesNotReplaceIt()
    {
        var engine=new AutoTunnel();var dispatcher=new ControlDispatcher(engine,new Sessions());
        var request=Auto();
        Assert.True((await dispatcher.DispatchAsync("owner",request,default)).Success);
        Assert.True((await dispatcher.DispatchAsync("owner",request,default)).Success);
        Assert.True((await dispatcher.DispatchAsync("owner",Auto(),default)).Success);
        Assert.Equal(1,engine.Starts);Assert.Equal(0,engine.Stops);Assert.Equal("owner",engine.OwnerSid);
    }
    [Theory]
    [InlineData(TunnelPhase.Connected,false)]
    [InlineData(TunnelPhase.Connecting,false)]
    [InlineData(TunnelPhase.Reconnecting,false)]
    [InlineData(TunnelPhase.Checking,false)]
    [InlineData(TunnelPhase.Checking,true)]
    [InlineData(TunnelPhase.Stopping,false)]
    [InlineData(TunnelPhase.Faulted,false)]
    [InlineData(TunnelPhase.Disconnected,true)]
    public async Task ExistingOrProtectedStateReturnsItsObservationWithoutAForcedReplacement(TunnelPhase phase,bool protection)
    {
        var observed=new TunnelStatus(phase,DateTimeOffset.UtcNow,"Existing",ProtectionActive:protection);
        var engine=new AutoTunnel{Status=observed,OwnerSid="owner"};
        var reply=await new ControlDispatcher(engine,new Sessions()).DispatchAsync("owner",Auto(),default);
        Assert.True(reply.Success);Assert.Equal(observed,reply.Status);
        Assert.Equal(0,engine.Starts+engine.Stops);
    }
    [Fact]
    public async Task NoOpReceiptNeverConnectsWhenReplayedAfterExplicitOffAndCannotChangePayload()
    {
        var engine=new AutoTunnel();var dispatcher=new ControlDispatcher(engine,new Sessions());
        await dispatcher.DispatchAsync("owner",Request("connect",CoreFixtures.Plan()),default);
        var skipped=Auto();
        Assert.True((await dispatcher.DispatchAsync("owner",skipped,default)).Success);
        Assert.True((await dispatcher.DispatchAsync("owner",Request("disconnect"),default)).Success);
        var replay=await dispatcher.DispatchAsync("owner",skipped,default);
        Assert.True(replay.Success);Assert.Equal(TunnelPhase.Disconnected,replay.Status.Phase);
        Assert.Equal(1,engine.Starts);Assert.Equal(1,engine.Stops);
        Assert.Equal("IPC_REPLAY_MISMATCH",(await dispatcher.DispatchAsync("owner",skipped with{Action="connect"},default)).Code);
        Assert.Equal("IPC_REPLAY_MISMATCH",(await dispatcher.DispatchAsync("owner",skipped with{Plan=CoreFixtures.Plan(RoutingMode.WholeComputer)},default)).Code);
        Assert.Equal("IPC_REPLAY_MISMATCH",(await dispatcher.DispatchAsync("other",skipped,default)).Code);
        Assert.Equal(1,engine.Starts);
    }
    [Fact]
    public async Task NoOpDoesNotReserveAnUnownedEngine()
    {
        var engine=new AutoTunnel{Status=new(TunnelPhase.Faulted,DateTimeOffset.UtcNow)};
        var dispatcher=new ControlDispatcher(engine,new Sessions());
        Assert.True((await dispatcher.DispatchAsync("first",Auto(),default)).Success);
        Assert.Null(engine.OwnerSid);
        engine.Status=new(TunnelPhase.Disconnected,DateTimeOffset.UtcNow);
        Assert.True((await dispatcher.DispatchAsync("second",Auto(),default)).Success);
        Assert.Equal("second",engine.OwnerSid);Assert.Equal(1,engine.Starts);
    }
    [Fact]
    public async Task MissingOrInvalidPlanIsRejectedEvenWhenTheEngineWouldBeANoOp()
    {
        var engine=new AutoTunnel{Status=new(TunnelPhase.Connected,DateTimeOffset.UtcNow),OwnerSid="owner"};
        var dispatcher=new ControlDispatcher(engine,new Sessions());
        Assert.Equal("IPC_INVALID",(await dispatcher.DispatchAsync("owner",Request("connect-if-idle"),default)).Code);
        Assert.Equal("INVALID_APPLICATION",(await dispatcher.DispatchAsync("owner",Auto() with{Plan=CoreFixtures.Plan() with{Applications=[]}},default)).Code);
        Assert.Equal(0,engine.Starts+engine.Stops);
    }
    [Fact]
    public async Task AutoRequestStillRequiresTheInteractiveSessionGuardBeforeANoOp()
    {
        var engine=new AutoTunnel{Status=new(TunnelPhase.Reconnecting,DateTimeOffset.UtcNow),OwnerSid="owner"};
        var dispatcher=new ControlDispatcher(engine,new Sessions{Reject=true});
        Assert.Equal("MULTI_SESSION_UNSUPPORTED",(await dispatcher.DispatchAsync("owner",Auto(),default)).Code);
        Assert.Equal(0,engine.Starts+engine.Stops);
    }
    [Fact]
    public async Task AnotherSidCannotUseAutoConnectToReadTheOwnedConnection()
    {
        var engine=new AutoTunnel{Status=new(TunnelPhase.Connected,DateTimeOffset.UtcNow,"Private profile"),OwnerSid="owner"};
        var reply=await new ControlDispatcher(engine,new Sessions()).DispatchAsync("other",Auto(),default);
        Assert.False(reply.Success);Assert.Equal("CONTROLLER_OWNED",reply.Code);Assert.Null(reply.Status.ProfileName);
        Assert.Equal(0,engine.Starts+engine.Stops);
    }
    private static PipeRequest Auto()=>Request("connect-if-idle",CoreFixtures.Plan());
    private static PipeRequest Request(string action,ConnectPlan? plan=null)=>new(PipeContract.Version,action,Guid.NewGuid(),plan);
    private sealed class Sessions : ISessionGuard
    {
        public bool Reject{get;init;}
        public void RequireOwner(string sid){if(Reject)throw new ClientFault("MULTI_SESSION_UNSUPPORTED");}
    }
    private sealed class AutoTunnel : IControlTunnelEngine
    {
        public int Starts{get;private set;}
        public int Stops{get;private set;}
        public string? OwnerSid{get;set;}
        public TunnelStatus Status{get;set;}=new(TunnelPhase.Disconnected,DateTimeOffset.UtcNow);
        public Task StartAsync(ConnectPlan plan,string sid,CancellationToken token)
        {
            Starts++;OwnerSid=sid;Status=new(TunnelPhase.Connected,DateTimeOffset.UtcNow,plan.Profile.Name);return Task.CompletedTask;
        }
        public Task StopAsync(CancellationToken token)
        {
            Stops++;OwnerSid=null;Status=new(TunnelPhase.Disconnected,DateTimeOffset.UtcNow);return Task.CompletedTask;
        }
        public Task ReconnectAsync(CancellationToken token)=>throw new InvalidOperationException("Auto-connect must use a validated plan.");
        public Task UpdateApplicationsAsync(ConnectPlan plan,string sid,CancellationToken token)=>throw new InvalidOperationException("Not an automatic connection.");
    }
}
