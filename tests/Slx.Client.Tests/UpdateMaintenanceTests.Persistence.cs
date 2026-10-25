using Slx.Client.Core;
using Slx.Client.Service;
using Xunit;
namespace Slx.Client.Tests;

public sealed partial class UpdateMaintenanceTests
{
    [Fact]
    public async Task RepeatedPrepareAndClaimDoNotExtendOrDowngradeLease()
    {
        var fixture = new Fixture();
        var request = fixture.Request("prepare-update");
        Assert.True((await fixture.Dispatcher.DispatchAsync("owner", request, default)).Success);
        var preparedExpiry = fixture.Store.Value!.ExpiresAt;
        fixture.Now += TimeSpan.FromMinutes(1);
        Assert.True((await fixture.Send("prepare-update")).Success);
        Assert.Equal(preparedExpiry, fixture.Store.Value.ExpiresAt);
        Assert.True((await fixture.Send("claim-update", admin: true)).Success);
        var claimedExpiry = fixture.Store.Value.ExpiresAt;
        fixture.Now += TimeSpan.FromMinutes(1);
        Assert.True((await fixture.Send("claim-update", admin: true)).Success);
        Assert.True((await fixture.Send("prepare-update")).Success);
        Assert.True(fixture.Store.Value.Claimed);
        Assert.Equal(claimedExpiry, fixture.Store.Value.ExpiresAt);
        Assert.Equal("IPC_REPLAY_MISMATCH", (await fixture.Dispatcher.DispatchAsync("owner",
            request with { UpdateSessionId = Guid.NewGuid() }, default)).Code);
    }
    [Fact]
    public async Task ClaimedLeaseSurvivesRestartAndElapsedDeadlineRequiresExplicitOwnerRelease()
    {
        var fixture = new Fixture();
        await fixture.Send("prepare-update");
        await fixture.Send("claim-update", admin: true);
        var maintenance = new UpdateMaintenance(fixture.Store) { UtcNow = () => fixture.Now };
        var dispatcher = new ControlDispatcher(fixture.Engine, fixture.Sessions) { Maintenance = maintenance };
        Assert.True(dispatcher.LoadMaintenance());
        Assert.Equal("UPDATE_MAINTENANCE_ACTIVE", (await dispatcher.DispatchAsync("owner",
            fixture.Request("connect", plan: CoreFixtures.Plan()), default)).Code);
        Assert.True((await dispatcher.DispatchAsync("owner", fixture.Request("status"), default)).Status.UpdateMaintenance!.Claimed);
        fixture.Now += TimeSpan.FromMinutes(16);
        Assert.Equal("UPDATE_MAINTENANCE_UNCONFIRMED", maintenance.BlockCode());
        Assert.NotNull(fixture.Store.Value);
        Assert.True(maintenance.Observation!.Claimed);
        Assert.Equal("UPDATE_MAINTENANCE_UNCONFIRMED", (await dispatcher.DispatchAsync("owner",
            fixture.Request("claim-update"), default, true)).Code);
        Assert.Equal("UPDATE_MAINTENANCE_UNCONFIRMED", (await dispatcher.DispatchAsync("owner",
            fixture.Request("connect", plan: CoreFixtures.Plan()), default)).Code);
        Assert.Equal(0, fixture.Engine.Starts);
        Assert.Equal("UPDATE_SESSION_MISMATCH", (await dispatcher.DispatchAsync("owner",
            fixture.Request("release-update", Guid.NewGuid()), default)).Code);
        fixture.Engine.ConfirmedOff = false;
        Assert.Equal("UPDATE_CONNECTION_ACTIVE", (await dispatcher.DispatchAsync("owner",
            fixture.Request("release-update"), default)).Code);
        fixture.Engine.ConfirmedOff = true;
        Assert.True((await dispatcher.DispatchAsync("owner", fixture.Request("release-update"), default)).Success);
        Assert.Null(fixture.Store.Value);
    }
    [Fact]
    public async Task CorruptOrFailedPersistenceBlocksConnectButNeverStatusOrExplicitOff()
    {
        foreach (bool loadFailure in new[] { true, false })
        {
            var fixture = new Fixture();
            if (loadFailure) { fixture.Store.FailLoad = true; Assert.True(fixture.Dispatcher.LoadMaintenance()); }
            else { fixture.Store.FailSave = true; Assert.False((await fixture.Send("prepare-update")).Success); }
            var status = await fixture.Send("status");
            Assert.True(status.Success);
            Assert.Equal("UPDATE_MAINTENANCE_UNCONFIRMED", status.Status.Code);
            Assert.Equal("UPDATE_MAINTENANCE_UNCONFIRMED", (await fixture.Send("connect", plan: CoreFixtures.Plan())).Code);
            Assert.True((await fixture.Send("disconnect")).Success);
            Assert.Equal(1, fixture.Engine.Stops);
            Assert.Equal(0, fixture.Engine.Starts);
        }
    }
    [Fact]
    public async Task ExpiredMarkerStillSuppressesBlindStartupRestoreAndFailedDeletionFailsClosed()
    {
        var fixture = new Fixture();
        await fixture.Send("prepare-update");
        fixture.Now += TimeSpan.FromMinutes(6);
        Assert.True(fixture.Dispatcher.LoadMaintenance());
        Assert.Null(fixture.Store.Value);
        Assert.Null(fixture.Maintenance.BlockCode());
        await fixture.Send("prepare-update");
        fixture.Now += TimeSpan.FromMinutes(6);
        fixture.Store.FailSave = true;
        Assert.Throws<ClientFault>(() => fixture.Maintenance.BlockCode());
        Assert.True(fixture.Maintenance.IsUnconfirmed);
        Assert.Equal(0, fixture.Engine.Starts);
    }
    [Fact]
    public async Task PrepareCannotPassAConcurrentConnectionThroughTheMutationGate()
    {
        var fixture = new Fixture();
        fixture.Engine.StartEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Engine.CompleteStart = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var connect = fixture.Send("connect", plan: CoreFixtures.Plan());
        await fixture.Engine.StartEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var prepare = fixture.Send("prepare-update");
        Assert.False(prepare.IsCompleted);
        fixture.Engine.CompleteStart.SetResult();
        Assert.True((await connect).Success);
        Assert.Equal("UPDATE_CONNECTION_ACTIVE", (await prepare).Code);
        Assert.Null(fixture.Store.Value);
    }
    [Fact]
    public async Task FullReplayCacheCannotStrandAnExactMaintenanceRelease()
    {
        var fixture = new Fixture();
        for (int i = 0; i < ControlDispatcher.ReceiptCapacity; i++)
            Assert.True((await fixture.Send("prepare-update")).Success);
        Assert.Equal("IPC_RECEIPT_LIMIT", (await fixture.Send("claim-update", admin: true)).Code);
        Assert.Equal("UPDATE_SESSION_MISMATCH", (await fixture.Send("release-update", id: Guid.NewGuid())).Code);
        Assert.True((await fixture.Send("release-update")).Success);
        Assert.Null(fixture.Store.Value);
        Assert.True((await fixture.Send("release-update")).Success);
    }
    [Fact]
    public async Task LargeBackwardClockChangeCannotExtendAConfirmedClaim()
    {
        var fixture = new Fixture();
        await fixture.Send("prepare-update");
        await fixture.Send("claim-update", admin: true);
        fixture.Now -= TimeSpan.FromMinutes(2);
        Assert.Equal("UPDATE_MAINTENANCE_UNCONFIRMED", (await fixture.Send("connect", plan: CoreFixtures.Plan())).Code);
        Assert.True((await fixture.Send("disconnect")).Success);
        Assert.NotNull(fixture.Store.Value);
        Assert.Equal(0, fixture.Engine.Starts);
    }
}
