using Slx.Client.Core;

namespace Slx.Client.Service;

internal readonly record struct CoreRecoveryResources(bool FrontAlive, bool AdapterReady,
    bool ProbeAvailable, bool LinkAvailable);
internal readonly record struct CoreRecoveryDecision(TunnelLifecycleScope Scope, TunnelLifecycleReason Reason);

/// <summary>A VPN-core failure is not evidence that the shared TUN or its direct flows must be destroyed.</summary>
internal sealed class CoreRecoveryPolicy
{
    internal TunnelLifecycleReason? RequiredFullReason { get; private set; }

    internal void RequireFull(TunnelLifecycleReason reason)
    {
        if (reason is not (TunnelLifecycleReason.ProtectionBaselineInvalid or TunnelLifecycleReason.TrafficRouteFailed
            or TunnelLifecycleReason.PhysicalInterfaceChanged or TunnelLifecycleReason.LocalLinkUnavailable))
            throw new ArgumentOutOfRangeException(nameof(reason));
        // A route check cannot make an uncertain native protection baseline valid again.
        if (RequiredFullReason != TunnelLifecycleReason.ProtectionBaselineInvalid) RequiredFullReason = reason;
    }

    internal void FullBaselineRestored() => RequiredFullReason = null;

    internal CoreRecoveryDecision Choose(CoreRecoveryResources resources, TunnelLifecycleReason reason)
    {
        if (RequiredFullReason is { } required) return new(TunnelLifecycleScope.FullTunnel, required);
        if (!resources.FrontAlive) return new(TunnelLifecycleScope.FullTunnel,
            reason == TunnelLifecycleReason.StartupResume ? reason : TunnelLifecycleReason.FrontUnavailable);
        if (!resources.AdapterReady) return new(TunnelLifecycleScope.FullTunnel, TunnelLifecycleReason.AdapterUnavailable);
        if (!resources.ProbeAvailable || !resources.LinkAvailable)
            return new(TunnelLifecycleScope.FullTunnel, TunnelLifecycleReason.LocalLinkUnavailable);
        return new(TunnelLifecycleScope.VpnCore, reason);
    }

    internal static TunnelLifecycleReason ReasonFor(string code) => code switch
    {
        "HEALTH_PROBE_FAILED" or "VPN_PROBE_FAILED" => TunnelLifecycleReason.HealthProbeFailed,
        "CORE_EXITED" => TunnelLifecycleReason.CoreExited,
        "TUN_NOT_READY" or "TUN_ADAPTER_CONFLICT" or "TUN_CLEANUP_PENDING" => TunnelLifecycleReason.AdapterUnavailable,
        TrafficRouteProbe.FailureCode => TunnelLifecycleReason.TrafficRouteFailed,
        _ when code.StartsWith("TRAFFIC_PROTECTION_", StringComparison.Ordinal) => TunnelLifecycleReason.ProtectionBaselineInvalid,
        _ => TunnelLifecycleReason.CoreFailure
    };
}
