using Slx.Client.Core;
using Slx.Client.Service;
using Xunit;

namespace Slx.Client.Tests;

public sealed class ServiceResponsivenessTests
{
    [Fact]
    public async Task StatusAndExplicitOffRemainResponsiveDuringConnect()
    {
        var engine = new WaitingTunnel();
        var dispatcher = new ControlDispatcher(engine, new Sessions());
        var connect = dispatcher.DispatchAsync("owner", Request("connect", CoreFixtures.Plan()), default);
        await engine.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var status = await dispatcher.DispatchAsync("owner", Request("status"), default).WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(status.Success);
        Assert.Equal(TunnelPhase.Connecting, status.Status.Phase);
        Assert.Equal("CONTROLLER_OWNED", (await dispatcher.DispatchAsync("other", Request("status"), default)).Code);
        var off = await dispatcher.DispatchAsync("owner", Request("disconnect"), default).WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(off.Success);
        Assert.Equal(TunnelPhase.Disconnected, off.Status.Phase);
        Assert.Equal("OPERATION_INTERRUPTED", (await connect).Code);
        Assert.Equal(1, engine.Stops);
    }

    [Fact]
    public async Task InvalidUnauthorizedAndMismatchedOffCannotCancelAnActiveRequest()
    {
        var engine = new WaitingTunnel();
        var dispatcher = new ControlDispatcher(engine, new Sessions());
        var request = Request("connect", CoreFixtures.Plan());
        var connect = dispatcher.DispatchAsync("owner", request, default);
        await engine.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal("CONTROLLER_OWNED", (await dispatcher.DispatchAsync("other", Request("disconnect"), default)).Code);
        Assert.Equal("IPC_INVALID", (await dispatcher.DispatchAsync("owner", Request("disconnect", CoreFixtures.Plan()), default)).Code);
        Assert.Equal("IPC_INVALID", (await dispatcher.DispatchAsync("owner", Request("disconnect") with { Version = 999 }, default)).Code);
        Assert.Equal("IPC_REPLAY_MISMATCH", (await dispatcher.DispatchAsync("owner", request with { Action = "disconnect", Plan = null }, default)).Code);
        Assert.False(connect.IsCompleted);
        Assert.Equal(0, engine.Stops);
        await dispatcher.DispatchAsync("owner", Request("disconnect"), default).WaitAsync(TimeSpan.FromSeconds(2));
        await connect;
    }

    [Fact]
    public async Task ReplayingAnOldOffDoesNotCancelANewerConnection()
    {
        var engine = new WaitingTunnel();
        var dispatcher = new ControlDispatcher(engine, new Sessions());
        var oldOff = Request("disconnect");
        Assert.True((await dispatcher.DispatchAsync("owner", oldOff, default)).Success);
        var connect = dispatcher.DispatchAsync("owner", Request("connect", CoreFixtures.Plan()), default);
        await engine.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var replay = await dispatcher.DispatchAsync("owner", oldOff, default).WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(replay.Success);
        Assert.Equal(TunnelPhase.Connecting, replay.Status.Phase);
        Assert.False(connect.IsCompleted);
        Assert.Equal(1, engine.Stops);
        await dispatcher.DispatchAsync("owner", Request("disconnect"), default).WaitAsync(TimeSpan.FromSeconds(2));
        await connect;
    }

    [Fact]
    public async Task OffAdmittedDuringValidationCancelsTheStartBeforeItsNetworkWork()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        var engine = new WaitingTunnel();
        var dispatcher = new ControlDispatcher(engine, new Sessions(() =>
        {
            entered.SetResult();
            Assert.True(release.Wait(TimeSpan.FromSeconds(5)));
        }));
        var connect = DispatchOnDedicatedThread(dispatcher, Request("connect", CoreFixtures.Plan()));
        Task<PipeReply> off;
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            off = dispatcher.DispatchAsync("owner", Request("disconnect"), default);
        }
        finally { release.Set(); }
        Assert.True((await off.WaitAsync(TimeSpan.FromSeconds(2))).Success);
        Assert.Equal("OPERATION_INTERRUPTED", (await connect).Code);
        Assert.False(engine.Started.Task.IsCompleted);
        Assert.Equal(1, engine.Stops);
    }

    [Fact]
    public async Task MismatchedOffDuringValidationCannotCancelTheAdmittedConnect()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        var engine = new WaitingTunnel();
        var dispatcher = new ControlDispatcher(engine, new Sessions(() =>
        {
            entered.SetResult();
            Assert.True(release.Wait(TimeSpan.FromSeconds(5)));
        }));
        var request = Request("connect", CoreFixtures.Plan());
        var connect = DispatchOnDedicatedThread(dispatcher, request);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            var mismatch = await dispatcher.DispatchAsync("owner", request with { Action = "disconnect", Plan = null }, default)
                .WaitAsync(TimeSpan.FromSeconds(2));
            Assert.Equal("IPC_REPLAY_MISMATCH", mismatch.Code);
        }
        finally { release.Set(); }
        await engine.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.False(connect.IsCompleted);
        Assert.Equal(0, engine.Stops);
        Assert.True((await dispatcher.DispatchAsync("owner", Request("disconnect"), default)
            .WaitAsync(TimeSpan.FromSeconds(2))).Success);
        Assert.Equal("OPERATION_INTERRUPTED", (await connect).Code);
    }

    // The session guard is synchronous. Do not block one pool worker while waiting for
    // another pool worker to reach it: a parallel test run can starve that handshake.
    private static Task<PipeReply> DispatchOnDedicatedThread(ControlDispatcher dispatcher, PipeRequest request) =>
        Task.Factory.StartNew(() => dispatcher.DispatchAsync("owner", request, default), CancellationToken.None,
            TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();

    private static PipeRequest Request(string action, ConnectPlan? plan = null) => new(PipeContract.Version, action, Guid.NewGuid(), plan);
    private sealed class Sessions(Action? check = null) : ISessionGuard
    {
        public void RequireOwner(string sid) => check?.Invoke();
    }
    private sealed class WaitingTunnel : IControlTunnelEngine
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Stops { get; private set; }
        public string? OwnerSid { get; private set; }
        public TunnelStatus Status { get; private set; } = new(TunnelPhase.Disconnected, DateTimeOffset.UtcNow);
        public async Task StartAsync(ConnectPlan plan, string sid, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            OwnerSid = sid;
            Status = new(TunnelPhase.Connecting, DateTimeOffset.UtcNow, plan.Profile.Name, ProtectionActive: true);
            Started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
        public Task ReconnectAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task UpdateApplicationsAsync(ConnectPlan plan, string sid, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task StopAsync(CancellationToken cancellationToken)
        {
            Stops++;
            OwnerSid = null;
            Status = new(TunnelPhase.Disconnected, DateTimeOffset.UtcNow);
            return Task.CompletedTask;
        }
    }
}
