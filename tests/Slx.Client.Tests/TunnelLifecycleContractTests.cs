using System.Text.Json;
using Slx.Client.Core;
using Slx.Client.Windows;
using Xunit;

namespace Slx.Client.Tests;

public sealed class TunnelLifecycleContractTests
{
    private static readonly DateTimeOffset Since = new(2026, 11, 6, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task BoundedHistoryRoundTripsThroughTheStrictPipe()
    {
        var events = Enumerable.Range(1, TunnelLifecycleObservation.MaximumEvents).Select(sequence =>
            new TunnelLifecycleEvent(sequence, Since.AddSeconds(sequence), TunnelLifecycleOrigin.AutomaticRecovery,
                TunnelLifecycleScope.VpnCore, TunnelLifecycleReason.HealthProbeFailed,
                sequence % 2 == 0 ? TunnelLifecycleOutcome.Connected : TunnelLifecycleOutcome.Started,
                1, sequence / 2 + 1, "HEALTH_PROBE_FAILED")).ToArray();
        var history = new TunnelLifecycleObservation(Since, 1, 9, events);
        var status = new TunnelStatus(TunnelPhase.Connected, Since.AddMinutes(1), ProbePassed: true,
            ProtectionActive: true, Lifecycle: history, LastHealthyAt: Since,
            NetworkPath: new(Since, PhysicalPathState.Ready, Since, true),
            TransportExecution: new(new("hysteria2", 1, 2, 1, Since), null, null));
        var reply = new PipeReply(true, "OK", status);
        using var stream = new MemoryStream();
        await PipeFraming.WriteAsync(stream, reply);
        Assert.InRange(stream.Length, 1, PipeContract.MaxBytes);
        stream.Position = 0;
        var received = await PipeFraming.ReadAsync<PipeReply>(stream);
        Assert.Equal(status with { Lifecycle = null }, received.Status with { Lifecycle = null });
        var restored = Assert.IsType<TunnelLifecycleObservation>(received.Status.Lifecycle);
        Assert.Equal((history.Since, history.TunGeneration, history.CoreGeneration),
            (restored.Since, restored.TunGeneration, restored.CoreGeneration));
        Assert.Equal(events, restored.Events.ToArray());
    }

    [Fact]
    public async Task IdleReplyOmitsNewHistoryAndIsReadableByTheOlderStrictUpdaterShape()
    {
        var idle = new TunnelStatus(TunnelPhase.Disconnected, Since, ServiceVersion: "0.1.0-alpha.28");
        var json = JsonSerializer.Serialize(idle);
        Assert.DoesNotContain("Lifecycle", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Attempt", json, StringComparison.Ordinal);
        Assert.DoesNotContain("NetworkPath", json, StringComparison.Ordinal);
        Assert.DoesNotContain("TransportExecution", json, StringComparison.Ordinal);
        Assert.DoesNotContain("LastHealthyAt", json, StringComparison.Ordinal);
        using var stream = new MemoryStream();
        await PipeFraming.WriteAsync(stream, new PipeReply(true, "OK", idle));
        stream.Position = 0;
        var legacy = await PipeFraming.ReadAsync<LegacyReply>(stream);
        Assert.True(legacy.Success);
        Assert.Equal(TunnelPhase.Disconnected, legacy.Status.Phase);
        Assert.False(legacy.Status.ProtectionActive);
        Assert.Equal(idle.ServiceVersion, legacy.Status.ServiceVersion);
    }

    [Fact]
    public async Task EarlierActiveStatusWithoutLifecycleRemainsReadable()
    {
        using var stream = new MemoryStream();
        await PipeFraming.WriteAsync(stream, new { Phase = 2, ObservedAt = Since, ProbePassed = true, ProtectionActive = true });
        stream.Position = 0;
        var status = await PipeFraming.ReadAsync<TunnelStatus>(stream);
        Assert.Equal(TunnelPhase.Connected, status.Phase);
        Assert.True(status.ProbePassed);
        Assert.Null(status.Lifecycle);
    }

    [Fact]
    public async Task UnknownLifecycleMembersStillFailStrictDecoding()
    {
        var entry = new TunnelLifecycleEvent(1, Since, TunnelLifecycleOrigin.ServiceRestore,
            TunnelLifecycleScope.FullTunnel, TunnelLifecycleReason.StartupResume, TunnelLifecycleOutcome.Started, 0, 0);
        var status = new TunnelStatus(TunnelPhase.Reconnecting, Since,
            Lifecycle: new(Since, 0, 0, [entry]));
        var json = JsonSerializer.SerializeToNode(status)!;
        json["Lifecycle"]!["Events"]![0]!["UnrecognizedAddress"] = "private.invalid";
        using var stream = new MemoryStream();
        await PipeFraming.WriteAsync(stream, json);
        stream.Position = 0;
        var error = await Assert.ThrowsAsync<ClientFault>(() => PipeFraming.ReadAsync<TunnelStatus>(stream));
        Assert.Equal("IPC_INVALID", error.Code);
    }

    [Theory]
    [InlineData("CORE_START_FAILED", true)]
    [InlineData("RUNTIME_HASH_MISMATCH", true)]
    [InlineData("VPN_TRAFFIC_ROUTE_FAILED", true)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("https://private.invalid/sub/secret", false)]
    [InlineData("C:\\private\\profile.json", false)]
    [InlineData("CORE_START_FAILED\nsecret", false)]
    [InlineData("UNKNOWN_PRIVATE_VALUE", false)]
    [InlineData("TRAFFIC_PROTECTION_FILTERV4_80004005", false)]
    public void HistoryCodesUseAnExactPublicWhitelist(string? code, bool allowed) =>
        Assert.Equal(allowed ? code : null, TunnelLifecycleObservation.SafeCode(code));

    [Fact]
    public void OversizedCodeIsNeverCopied() => Assert.Null(
        TunnelLifecycleObservation.SafeCode(new string('A', TunnelLifecycleObservation.MaximumCodeLength + 1)));

    // The alpha.25/26 staged updater has neither Attempt nor Lifecycle and disallows unknown members.
    private sealed record LegacyReply(bool Success, string Code, LegacyStatus Status);
    private sealed record LegacyStatus(TunnelPhase Phase, DateTimeOffset ObservedAt,
        string? ProfileName = null, string? Code = null, DateTimeOffset? ConnectedAt = null,
        RoutingMode? Mode = null, bool ProbePassed = false, bool ProtectionActive = false,
        RecoveryObservation? Recovery = null, HealthCheckObservation? HealthCheck = null,
        string? ApplicationRevision = null, DnsCheckObservation? DnsCheck = null,
        TrafficRouteCheckObservation? TrafficRoutes = null, string? ServiceVersion = null,
        UpdateMaintenanceObservation? UpdateMaintenance = null);
}
