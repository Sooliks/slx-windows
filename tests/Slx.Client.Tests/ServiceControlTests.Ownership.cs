using Slx.Client.Core;
using Slx.Client.Service;
using Xunit;
namespace Slx.Client.Tests;
public sealed partial class ServiceControlTests
{
    [Fact]
    public async Task ConfirmedExplicitOffAllowsTheNextWindowsOwnerWithoutReplayingPriorEffects()
    {
        var engine=new MemoryTunnel();var dispatcher=new ControlDispatcher(engine,new Sessions());
        var oldConnect=Request("connect",CoreFixtures.Plan());
        Assert.True((await dispatcher.DispatchAsync("first",oldConnect,default)).Success);
        Assert.True((await dispatcher.DispatchAsync("first",Request("disconnect"),default)).Success);
        Assert.Null(engine.OwnerSid);
        Assert.Equal("IPC_REPLAY_MISMATCH",(await dispatcher.DispatchAsync("second",oldConnect,default)).Code);
        var replay=await dispatcher.DispatchAsync("first",oldConnect,default);
        Assert.Equal(TunnelPhase.Disconnected,replay.Status.Phase);Assert.Equal(1,engine.Starts);
        Assert.True((await dispatcher.DispatchAsync("second",Request("connect",CoreFixtures.Plan()),default)).Success);
        Assert.Equal("second",engine.OwnerSid);
        Assert.Equal("CONTROLLER_OWNED",(await dispatcher.DispatchAsync("first",oldConnect,default)).Code);
        Assert.Equal(2,engine.Starts);
    }
    [Fact]
    public async Task FailedOffRetainsOwnerUntilASuccessfulRetry()
    {
        var engine=new MemoryTunnel{StopFails=true};var dispatcher=new ControlDispatcher(engine,new Sessions());
        await dispatcher.DispatchAsync("owner",Request("connect",CoreFixtures.Plan()),default);
        var failed=await dispatcher.DispatchAsync("owner",Request("disconnect"),default);
        Assert.False(failed.Success);Assert.Equal("CORE_STOP_FAILED",failed.Code);
        Assert.Equal("CONTROLLER_OWNED",(await dispatcher.DispatchAsync("other",Request("status"),default)).Code);
        engine.StopFails=false;
        Assert.True((await dispatcher.DispatchAsync("owner",Request("disconnect"),default)).Success);
        Assert.True((await dispatcher.DispatchAsync("other",Request("connect",CoreFixtures.Plan()),default)).Success);
    }
    [Theory]
    [InlineData(TunnelPhase.Disconnected,true)]
    [InlineData(TunnelPhase.Reconnecting,false)]
    [InlineData(TunnelPhase.Faulted,false)]
    public async Task StopWithoutConfirmedUnprotectedDisconnectedObservationCannotTransferOwner(TunnelPhase phase,bool protectedTraffic)
    {
        var engine=new MemoryTunnel{ReportUnownedAfterStop=true,StopObservation=new(phase,DateTimeOffset.UtcNow,ProtectionActive:protectedTraffic)};
        var dispatcher=new ControlDispatcher(engine,new Sessions());
        await dispatcher.DispatchAsync("owner",Request("connect",CoreFixtures.Plan()),default);
        await dispatcher.DispatchAsync("owner",Request("disconnect"),default);
        Assert.Equal("CONTROLLER_OWNED",(await dispatcher.DispatchAsync("other",Request("connect",CoreFixtures.Plan()),default)).Code);
    }
    [Fact]
    public async Task SaturatedReceiptsPreserveExplicitOffAndExistingReplayChecks()
    {
        var engine=new MemoryTunnel();var dispatcher=new ControlDispatcher(engine,new Sessions());
        var first=Request("connect",CoreFixtures.Plan());
        await dispatcher.DispatchAsync("owner",first,default);
        for(int i=1;i<4096;i++)Assert.True((await dispatcher.DispatchAsync("owner",Request("reconnect",CoreFixtures.Plan()),default)).Success);
        Assert.Equal("IPC_RECEIPT_LIMIT",(await dispatcher.DispatchAsync("owner",Request("connect",CoreFixtures.Plan()),default)).Code);
        Assert.Equal("IPC_REPLAY_MISMATCH",(await dispatcher.DispatchAsync("owner",first with{Action="disconnect",Plan=null},default)).Code);
        Assert.Equal("IPC_INVALID",(await dispatcher.DispatchAsync("owner",Request("disconnect",CoreFixtures.Plan()),default)).Code);
        Assert.Equal(0,engine.Stops);
        var off=Request("disconnect");
        Assert.True((await dispatcher.DispatchAsync("owner",off,default)).Success);
        Assert.Equal(TunnelPhase.Disconnected,engine.Status.Phase);Assert.Null(engine.OwnerSid);
        Assert.True((await dispatcher.DispatchAsync("owner",off,default)).Success);
        Assert.Equal(2,engine.Stops); // Uncached explicit off remains safe and idempotent.
        Assert.Equal("IPC_RECEIPT_LIMIT",(await dispatcher.DispatchAsync("other",Request("connect",CoreFixtures.Plan()),default)).Code);
        Assert.Equal("IPC_REPLAY_MISMATCH",(await dispatcher.DispatchAsync("other",first,default)).Code);
    }
}
