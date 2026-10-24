namespace Slx.Client.Core;

public static class PipeContract
{
    public const int Version = 2;
    public const string Name = "SLX.VPN.Control.v2";
    public const int MaxBytes = 65536;
    // Leave room for the service's bounded startup/probe/cleanup work. A separate pipe slot can
    // still request explicit off immediately; timeout never authorizes replaying a mutation.
    public static readonly TimeSpan ClientStatusTimeout = TimeSpan.FromSeconds(10);
    public static readonly TimeSpan ClientMutationTimeout = TimeSpan.FromSeconds(80);
}
public enum TunnelPhase { Disconnected, Connecting, Connected, Reconnecting, Stopping, Faulted }
public sealed record TunnelStatus(TunnelPhase Phase, DateTimeOffset ObservedAt,
    string? ProfileName = null, string? Code = null, DateTimeOffset? ConnectedAt = null,
    RoutingMode? Mode = null, bool ProbePassed = false, bool ProtectionActive = false, RecoveryObservation? Recovery = null,
    HealthCheckObservation? HealthCheck = null, string? ApplicationRevision = null);
public sealed record PipeRequest(int Version, string Action, Guid RequestId, ConnectPlan? Plan = null);
public sealed record PipeReply(bool Success, string Code, TunnelStatus Status);
