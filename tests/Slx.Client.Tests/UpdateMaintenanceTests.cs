using Slx.Client.Core;
using Slx.Client.Service;
using Slx.Client.Windows;
using Xunit;
namespace Slx.Client.Tests;

public sealed partial class UpdateMaintenanceTests
{
    [Fact]
    public async Task PreparedOffBlocksEveryConnectButKeepsStatusAndOff()
    {
        var fixture = new Fixture();
        var prepared = await fixture.Send("prepare-update");
        Assert.True(prepared.Success);
        Assert.Equal(fixture.Id, prepared.Status.UpdateMaintenance!.SessionId);
        Assert.False(prepared.Status.UpdateMaintenance.Claimed);
        Assert.NotNull(prepared.Status.ServiceVersion);
        foreach (string action in new[] { "connect", "connect-if-idle", "reconnect", "update-applications" })
        {
            var reply = await fixture.Send(action, plan: action == "reconnect" ? null : CoreFixtures.Plan());
            Assert.Equal("UPDATE_MAINTENANCE_ACTIVE", reply.Code);
        }
        Assert.True((await fixture.Send("status")).Success);
        Assert.True((await fixture.Send("disconnect")).Success);
        Assert.Equal(0, fixture.Engine.Starts);
        Assert.Equal(1, fixture.Engine.Stops);
        Assert.True((await fixture.Send("release-update")).Success);
        Assert.True((await fixture.Send("connect", plan: CoreFixtures.Plan())).Success);
    }
    [Fact]
    public async Task ActiveOrUncertainEngineCannotPrepareOrClaim()
    {
        var fixture = new Fixture();
        fixture.Engine.ConfirmedOff = false;
        Assert.Equal("UPDATE_CONNECTION_ACTIVE", (await fixture.Send("prepare-update")).Code);
        Assert.Null(fixture.Store.Value);
        fixture.Engine.ConfirmedOff = true;
        Assert.True((await fixture.Send("prepare-update")).Success);
        fixture.Engine.ConfirmedOff = false;
        Assert.Equal("UPDATE_CONNECTION_ACTIVE", (await fixture.Send("claim-update", admin: true)).Code);
        Assert.False(fixture.Store.Value!.Claimed);
    }
    [Fact]
    public async Task ElevatedOtherAdminCanClaimButCannotReleaseOriginalUsersSession()
    {
        var fixture = new Fixture();
        Assert.True((await fixture.Send("prepare-update")).Success);
        Assert.Equal("UPDATE_ADMIN_REQUIRED", (await fixture.Send("claim-update")).Code);
        Assert.Equal("CONTROLLER_OWNED", (await fixture.Send("claim-update", sid: "other")).Code);
        Assert.Equal("UPDATE_SESSION_MISMATCH", (await fixture.Send("claim-update", sid: "admin", admin: true, id: Guid.NewGuid())).Code);
        Assert.True((await fixture.Send("claim-update", sid: "admin", admin: true)).Success);
        Assert.Equal("owner", fixture.Store.Value!.OwnerSid);
        Assert.Equal("owner", fixture.Sessions.LastSid);
        Assert.Equal("CONTROLLER_OWNED", (await fixture.Send("release-update", sid: "admin", admin: true)).Code);
        Assert.Equal("CONTROLLER_OWNED", (await fixture.Send("status", sid: "other")).Code);
        Assert.True((await fixture.Send("release-update")).Success);
    }
    [Fact]
    public async Task StrictFieldsAndSessionGuardRejectBeforeStoreOrNetworkMutation()
    {
        var fixture = new Fixture();
        foreach (var request in new[]
        {
            fixture.Request("prepare-update") with { UpdateSessionId = null },
            fixture.Request("prepare-update") with { UpdateSessionId = Guid.Empty },
            fixture.Request("prepare-update") with { Plan = CoreFixtures.Plan() },
            fixture.Request("connect") with { Plan = CoreFixtures.Plan(), UpdateSessionId = fixture.Id },
            fixture.Request("status") with { UpdateSessionId = fixture.Id }
        }) Assert.Equal("IPC_INVALID", (await fixture.Dispatcher.DispatchAsync("owner", request, default)).Code);
        fixture.Sessions.Reject = true;
        Assert.Equal("MULTI_SESSION_UNSUPPORTED", (await fixture.Send("prepare-update")).Code);
        Assert.Equal(0, fixture.Store.Writes);
        Assert.Equal(0, fixture.Engine.Starts);
    }
    private sealed class Fixture
    {
        public Guid Id { get; } = Guid.NewGuid();
        public DateTimeOffset Now { get; set; } = DateTimeOffset.Parse("2026-10-25T16:00:00Z");
        public MemoryStore Store { get; } = new();
        public MemoryEngine Engine { get; } = new();
        public SessionGuard Sessions { get; } = new();
        public UpdateMaintenance Maintenance { get; }
        public ControlDispatcher Dispatcher { get; }
        public Fixture()
        {
            Maintenance = new(Store) { UtcNow = () => Now };
            Dispatcher = new(Engine, Sessions) { Maintenance = Maintenance };
        }
        public PipeRequest Request(string action, Guid? id = null, ConnectPlan? plan = null) => new(PipeContract.Version,
            action, Guid.NewGuid(), plan, action.EndsWith("-update", StringComparison.Ordinal) ? id ?? Id : null);
        public Task<PipeReply> Send(string action, string sid = "owner", bool admin = false, Guid? id = null, ConnectPlan? plan = null)
            => Dispatcher.DispatchAsync(sid, Request(action, id, plan), default, admin);
    }
    private sealed class MemoryStore : IUpdateMaintenanceStore
    {
        public ProtectedUpdateMaintenance? Value { get; set; }
        public bool FailLoad { get; set; }
        public bool FailSave { get; set; }
        public int Writes { get; private set; }
        public ProtectedUpdateMaintenance? LoadMaintenance() => FailLoad ? throw new ClientFault("PROTECTION_STORAGE_FAILED") : Value;
        public void SaveMaintenance(ProtectedUpdateMaintenance? value)
        { Writes++; if (FailSave) throw new IOException(); Value = value; }
    }
    private sealed class SessionGuard : ISessionGuard
    {
        public bool Reject { get; set; }
        public string? LastSid { get; private set; }
        public void RequireOwner(string sid)
        { LastSid = sid; if (Reject) throw new ClientFault("MULTI_SESSION_UNSUPPORTED"); }
    }
    private sealed class MemoryEngine : IControlTunnelEngine
    {
        public bool ConfirmedOff { get; set; } = true;
        public bool IsConfirmedOff => ConfirmedOff;
        public int Starts { get; private set; }
        public int Stops { get; private set; }
        public TaskCompletionSource? StartEntered { get; set; }
        public TaskCompletionSource? CompleteStart { get; set; }
        public TunnelStatus Status => new(ConfirmedOff ? TunnelPhase.Disconnected : TunnelPhase.Connected,
            DateTimeOffset.UtcNow, ProtectionActive: !ConfirmedOff);
        public async Task StartAsync(ConnectPlan plan, string sid, CancellationToken cancellationToken)
        {
            Starts++; ConfirmedOff = false; StartEntered?.TrySetResult();
            if (CompleteStart is not null) await CompleteStart.Task.WaitAsync(cancellationToken);
        }
        public Task ReconnectAsync(CancellationToken token) => throw new InvalidOperationException();
        public Task UpdateApplicationsAsync(ConnectPlan plan, string sid, CancellationToken token) => throw new InvalidOperationException();
        public Task StopAsync(CancellationToken token) { Stops++; ConfirmedOff = true; return Task.CompletedTask; }
    }
}
