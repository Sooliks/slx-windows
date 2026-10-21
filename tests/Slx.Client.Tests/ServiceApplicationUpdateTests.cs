using Slx.Client.Core;
using Slx.Client.Service;
using Xunit;
namespace Slx.Client.Tests;
public sealed partial class ServiceControlTests
{
    [Fact]
    public async Task RejectedIdleApplicationUpdateCannotReserveTheController()
    {
        var engine = new MemoryTunnel(); var dispatcher = new ControlDispatcher(engine, new Sessions());
        Assert.False((await dispatcher.DispatchAsync("first", Request("update-applications", CoreFixtures.Plan()), default)).Success);
        Assert.True((await dispatcher.DispatchAsync("second", Request("connect", CoreFixtures.Plan()), default)).Success);
        Assert.Equal("second", engine.OwnerSid); Assert.Equal(1, engine.Starts); Assert.Equal(0, engine.Updates);
    }
    [Fact]
    public async Task ApplicationUpdateHasItsOwnEnginePathAndStrictReplayFingerprint()
    {
        var engine = new MemoryTunnel();
        var dispatcher = new ControlDispatcher(engine, new Sessions());
        await dispatcher.DispatchAsync("owner", Request("connect", CoreFixtures.Plan()), default);
        var connectedAt = engine.Status.ConnectedAt;
        var request = Request("update-applications", CoreFixtures.Plan());
        Assert.True((await dispatcher.DispatchAsync("owner", request, default)).Success);
        Assert.True((await dispatcher.DispatchAsync("owner", request, default)).Success);
        Assert.Equal(1, engine.Updates); Assert.Equal(1, engine.Starts); Assert.Equal(0, engine.Stops);
        Assert.Equal(connectedAt, engine.Status.ConnectedAt);
        Assert.Equal("IPC_REPLAY_MISMATCH", (await dispatcher.DispatchAsync("owner", request with
        { Plan = request.Plan! with { Applications = [CoreFixtures.App(@"C:\Apps\Other.exe")] } }, default)).Code);
        await dispatcher.DispatchAsync("owner", Request("disconnect"), default);
        Assert.True((await dispatcher.DispatchAsync("owner", request, default)).Success);
        Assert.Equal(1, engine.Updates); Assert.Equal(TunnelPhase.Disconnected, engine.Status.Phase);
    }
    [Fact]
    public async Task ApplicationUpdatesRetainOwnerSessionAndPayloadGuards()
    {
        var engine = new MemoryTunnel();
        var dispatcher = new ControlDispatcher(engine, new Sessions());
        await dispatcher.DispatchAsync("owner", Request("connect", CoreFixtures.Plan()), default);
        Assert.Equal("CONTROLLER_OWNED", (await dispatcher.DispatchAsync("other",
            Request("update-applications", CoreFixtures.Plan()), default)).Code);
        Assert.Equal("IPC_INVALID", (await dispatcher.DispatchAsync("owner", Request("update-applications"), default)).Code);
        Assert.Equal("INVALID_APPLICATION", (await dispatcher.DispatchAsync("owner",
            Request("update-applications", CoreFixtures.Plan() with { Applications = [] }), default)).Code);
        Assert.Equal(0, engine.Updates);
        var rejected = new ControlDispatcher(engine, new Sessions { Reject = true });
        Assert.Equal("MULTI_SESSION_UNSUPPORTED", (await rejected.DispatchAsync("owner",
            Request("update-applications", CoreFixtures.Plan()), default)).Code);
        Assert.Equal(0, engine.Updates);
    }
    [Fact]
    public async Task ApplicationUpdatesCannotBypassReceiptCapacity()
    {
        var engine = new MemoryTunnel(); var dispatcher = new ControlDispatcher(engine, new Sessions());
        await dispatcher.DispatchAsync("owner", Request("connect", CoreFixtures.Plan()), default);
        for (int i = 1; i < 4096; i++)
            Assert.True((await dispatcher.DispatchAsync("owner", Request("update-applications", CoreFixtures.Plan()), default)).Success);
        Assert.Equal("IPC_RECEIPT_LIMIT", (await dispatcher.DispatchAsync("owner",
            Request("update-applications", CoreFixtures.Plan()), default)).Code);
        Assert.True((await dispatcher.DispatchAsync("owner", Request("disconnect"), default)).Success);
        Assert.Equal(1, engine.Starts); Assert.Equal(1, engine.Stops); Assert.Equal(4095, engine.Updates);
    }
}
