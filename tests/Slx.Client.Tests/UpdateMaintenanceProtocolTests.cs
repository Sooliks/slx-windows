using System.Text;
using Slx.Client.Core;
using Slx.Client.Service;
using Slx.Client.Windows;
using Xunit;
namespace Slx.Client.Tests;

public sealed class UpdateMaintenanceProtocolTests
{
    [Fact]
    public async Task OldRequestsHaveNoMaintenanceSessionAndNewSnapshotsRoundTripWithoutOwnerSid()
    {
        using var legacy = new MemoryStream();
        await PipeFraming.WriteAsync(legacy, new { Version = PipeContract.Version, Action = "status", RequestId = Guid.NewGuid(), Plan = (object?)null });
        legacy.Position = 0;
        Assert.Null((await PipeFraming.ReadAsync<PipeRequest>(legacy)).UpdateSessionId);
        var reply = new PipeReply(true, "OK", new(TunnelPhase.Disconnected, DateTimeOffset.UtcNow,
            ServiceVersion: "0.1.0-alpha.25", UpdateMaintenance: new(Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(5), false)));
        using var stream = new MemoryStream();
        await PipeFraming.WriteAsync(stream, reply);
        Assert.DoesNotContain("OwnerSid", Encoding.UTF8.GetString(stream.ToArray()[4..]));
        stream.Position = 0;
        Assert.Equal(reply, await PipeFraming.ReadAsync<PipeReply>(stream));
    }
    [Fact]
    public async Task UnknownPrivilegedPayloadFieldsRemainRejected()
    {
        using var stream = new MemoryStream();
        await PipeFraming.WriteAsync(stream, new { Version = PipeContract.Version, Action = "claim-update", RequestId = Guid.NewGuid(),
            UpdateSessionId = Guid.NewGuid(), IsAdministrator = true, Executable = "cmd.exe" });
        stream.Position = 0;
        Assert.Equal("IPC_INVALID", (await Assert.ThrowsAsync<ClientFault>(() => PipeFraming.ReadAsync<PipeRequest>(stream))).Code);
    }
    [Fact]
    public async Task FixedClaimCommandRequiresAdminStrictArgumentsAndConfirmedMatchingClaim()
    {
        var id = Guid.NewGuid();
        string[] args = ["--claim-update", id.ToString("N")];
        int sends = 0;
        Task<PipeReply> Send(PipeRequest request, CancellationToken token)
        {
            sends++;
            Assert.Equal("claim-update", request.Action); Assert.Equal(id, request.UpdateSessionId); Assert.Null(request.Plan);
            return Task.FromResult(new PipeReply(true, "OK", new(TunnelPhase.Disconnected, DateTimeOffset.UtcNow,
                UpdateMaintenance: new(id, DateTimeOffset.UtcNow.AddMinutes(15), true))));
        }
        Assert.Equal(2, await UpdateClaimCommand.RunAsync(args, false, Send));
        foreach (var invalid in new[] { new[] { "--claim-update" }, new[] { "--claim-update", Guid.Empty.ToString("N") },
            new[] { "--claim-update", id.ToString("D") }, new[] { "--claim-update", id.ToString("N"), "extra" } })
            Assert.Equal(2, await UpdateClaimCommand.RunAsync(invalid, true, Send));
        Assert.Equal(0, sends);
        Assert.Equal(0, await UpdateClaimCommand.RunAsync(args, true, Send));
        Assert.Equal(1, sends);
        foreach (var status in new[]
        {
            new TunnelStatus(TunnelPhase.Disconnected, DateTimeOffset.UtcNow),
            new(TunnelPhase.Connected, DateTimeOffset.UtcNow, UpdateMaintenance: new(id, DateTimeOffset.UtcNow.AddMinutes(5), true)),
            new(TunnelPhase.Disconnected, DateTimeOffset.UtcNow, ProtectionActive: true, UpdateMaintenance: new(id, DateTimeOffset.UtcNow.AddMinutes(5), true)),
            new(TunnelPhase.Disconnected, DateTimeOffset.UtcNow, UpdateMaintenance: new(Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(5), true)),
            new(TunnelPhase.Disconnected, DateTimeOffset.UtcNow, UpdateMaintenance: new(id, DateTimeOffset.UtcNow.AddMinutes(5), false))
        }) Assert.Equal(1, await UpdateClaimCommand.RunAsync(args, true, (_, _) => Task.FromResult(new PipeReply(true, "OK", status))));
        Assert.Equal(1, await UpdateClaimCommand.RunAsync(args, true, (_, _) => throw new ClientFault("SERVICE_UNAVAILABLE")));
    }
}
