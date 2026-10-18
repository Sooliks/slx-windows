using Slx.Client.Core;
using Slx.Client.Service;
using Xunit;

namespace Slx.Client.Tests;

/// <summary>Pure dispatcher contracts, not Windows ACL, service or real TUN acceptance.</summary>
public sealed class ServiceControlTests
{
    [Fact]
    public async Task SecondSidCannotReadOrControlProfile()
    {
        var engine = new MemoryTunnel();
        var dispatcher = new ControlDispatcher(engine, new Sessions());
        await dispatcher.DispatchAsync("owner", Request("connect", CoreFixtures.Plan()), default);
        foreach (string action in new[] { "status", "disconnect", "reconnect", "connect" })
        {
            var reply = await dispatcher.DispatchAsync("other", Request(action,
                action == "connect" ? CoreFixtures.Plan() : null), default);
            Assert.False(reply.Success);
            Assert.Equal("CONTROLLER_OWNED", reply.Code);
            Assert.Null(reply.Status.ProfileName);
            Assert.Null(reply.Status.Mode);
        }
        Assert.Equal(1, engine.Starts);
        Assert.Equal(0, engine.Stops);
    }

    [Fact]
    public async Task SameRequestCannotRepeatSideEffectAndChangedPayloadIsRejected()
    {
        var engine = new MemoryTunnel();
        var dispatcher = new ControlDispatcher(engine, new Sessions());
        var request = Request("connect", CoreFixtures.Plan());
        Assert.True((await dispatcher.DispatchAsync("owner", request, default)).Success);
        Assert.True((await dispatcher.DispatchAsync("owner", request, default)).Success);
        var changed = request with { Plan = request.Plan! with { Mode = RoutingMode.WholeComputer } };
        Assert.Equal("IPC_REPLAY_MISMATCH", (await dispatcher.DispatchAsync("owner", changed, default)).Code);
        Assert.Equal(1, engine.Starts);
        await dispatcher.DispatchAsync("owner", Request("disconnect"), default);
        var replay = await dispatcher.DispatchAsync("owner", request, default);
        Assert.Equal(TunnelPhase.Disconnected, replay.Status.Phase);
        Assert.Equal(1, engine.Starts);
    }

    [Fact]
    public async Task InvalidRequestsDoNotReachPrivilegedEngine()
    {
        var engine = new MemoryTunnel();
        var dispatcher = new ControlDispatcher(engine, new Sessions());
        var malformed = new[]
        {
            Request("shell"), Request("connect"), Request("disconnect", CoreFixtures.Plan()),
            Request("connect", CoreFixtures.Plan()) with { Version = 999 },
            Request("connect", CoreFixtures.Plan()) with { RequestId = Guid.Empty }
        };
        foreach (var request in malformed)
            Assert.False((await dispatcher.DispatchAsync("owner", request, default)).Success);
        Assert.Equal(0, engine.Starts + engine.Stops);
    }

    [Fact]
    public async Task SessionGuardRejectsConnectWithoutPrivilegedSideEffects()
    {
        var engine = new MemoryTunnel();
        var dispatcher = new ControlDispatcher(engine, new Sessions { Reject = true });
        var reply = await dispatcher.DispatchAsync("owner", Request("connect", CoreFixtures.Plan()), default);
        Assert.Equal("MULTI_SESSION_UNSUPPORTED", reply.Code);
        Assert.False(reply.Success);
        Assert.Equal(0, engine.Starts);
    }

    [Fact]
    public async Task ReconnectAppliesValidatedNewPlanOnlyOnce()
    {
        var engine = new MemoryTunnel();
        var dispatcher = new ControlDispatcher(engine, new Sessions());
        await dispatcher.DispatchAsync("owner", Request("connect", CoreFixtures.Plan()), default);
        var request = Request("reconnect", CoreFixtures.Plan(RoutingMode.WholeComputer));
        Assert.True((await dispatcher.DispatchAsync("owner", request, default)).Success);
        Assert.True((await dispatcher.DispatchAsync("owner", request, default)).Success);
        Assert.Equal(2, engine.Starts);
        Assert.Equal(RoutingMode.WholeComputer, engine.Status.Mode);
        var invalid = request with { RequestId = Guid.NewGuid(), Plan = CoreFixtures.Plan() with { Applications = [] } };
        Assert.False((await dispatcher.DispatchAsync("owner", invalid, default)).Success);
        Assert.Equal(2, engine.Starts);
    }

    private static PipeRequest Request(string action, ConnectPlan? plan = null) =>
        new(PipeContract.Version, action, Guid.NewGuid(), plan);

    private sealed class Sessions : ISessionGuard
    {
        public bool Reject { get; init; }
        public void RequireOwner(string sid)
        { if (Reject) throw new ClientFault("MULTI_SESSION_UNSUPPORTED"); }
    }

    private sealed class MemoryTunnel : IControlTunnelEngine
    {
        public int Starts { get; private set; }
        public int Stops { get; private set; }
        public TunnelStatus Status { get; private set; } = new(TunnelPhase.Disconnected, DateTimeOffset.UtcNow);
        public Task StartAsync(ConnectPlan plan, string sid, CancellationToken cancellationToken)
        {
            Starts++;
            Status = new(TunnelPhase.Connected, DateTimeOffset.UtcNow, plan.Profile.Name, Mode: plan.Mode);
            return Task.CompletedTask;
        }
        public Task ReconnectAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken)
        {
            Stops++;
            Status = new(TunnelPhase.Disconnected, DateTimeOffset.UtcNow);
            return Task.CompletedTask;
        }
    }
}
