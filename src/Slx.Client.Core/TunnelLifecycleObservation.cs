using System.Text.Json.Serialization;

namespace Slx.Client.Core;

// A request identifies the control path, not whether a person clicked a button.
public enum TunnelLifecycleOrigin { ConnectRequest, ReconnectRequest, ServiceRestore, AutomaticRecovery }
public enum TunnelLifecycleScope { FullTunnel, VpnCore }
public enum TunnelLifecycleReason
{
    Requested, StartupResume, HealthProbeFailed, CoreExited, FrontUnavailable, AdapterUnavailable,
    LocalLinkUnavailable, TrafficRouteFailed, ProtectionBaselineInvalid, CoreFailure
}
public enum TunnelLifecycleOutcome { Started, Connected, Failed, Cancelled }

/// <summary>One bounded lifecycle observation; generations count process starts, not PIDs or traffic.</summary>
public sealed record TunnelLifecycleEvent(long Sequence, DateTimeOffset At, TunnelLifecycleOrigin Origin,
    TunnelLifecycleScope Scope, TunnelLifecycleReason Reason, TunnelLifecycleOutcome Outcome,
    int TunGeneration, int CoreGeneration,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Code = null);

/// <summary>Recent in-memory service history. Older entries can be absent; no packet continuity is implied.</summary>
public sealed record TunnelLifecycleObservation(DateTimeOffset Since, int TunGeneration, int CoreGeneration,
    IReadOnlyList<TunnelLifecycleEvent> Events)
{
    public const int MaximumEvents = 16;
    public const int MaximumCodeLength = 64;

    // Unknown or native-specific errors remain outside this shared history; never copy raw exception text.
    public static string? SafeCode(string? code) => code is { Length: > 0 and <= MaximumCodeLength } && code is
        "CORE_EXITED" or "CORE_START_FAILED" or "CORE_STOP_FAILED" or "CORE_CONTAINMENT_FAILED"
        or "VPN_PROBE_FAILED" or "VPN_TRAFFIC_ROUTE_FAILED" or "VPN_DNS_FAILED" or "VPN_DNS_ROUTE_FAILED"
        or "TUN_NOT_READY" or "TUN_ADAPTER_CONFLICT" or "TUN_CLEANUP_PENDING" or "HEALTH_PROBE_FAILED"
        or "OUTBOUND_INTERFACE_UNAVAILABLE" or "SERVICE_FAILURE" or "OPERATION_INTERRUPTED"
        or "TRAFFIC_PROTECTION_FAILED" or "RUNTIME_INSTALL_LOCATION" or "RUNTIME_MANIFEST_INVALID"
        or "RUNTIME_HASH_MISMATCH" or "RUNTIME_UNSAFE_PATH" or "RUNTIME_UNSAFE_OWNER" or "RUNTIME_UNSAFE_ACL"
            ? code : null;
}
