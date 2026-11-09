using System.Diagnostics;
using Slx.Client.Core;
using Slx.Client.Windows;

namespace Slx.Client.Service;

internal sealed partial class TunnelEngine
{
    private OutboundBinding? outboundBinding;
    private volatile NetworkPathObservation? networkPath;

    private bool PhysicalPathReady()
    {
        if (!FrontAlive || outboundBinding is null || rotation.Count == 0) return true;
        var observed = PhysicalEgress.Read(rotation.Current.Host);
        var result = PhysicalPathPolicy.Compare(outboundBinding, observed);
        networkPath = new(DateTimeOffset.UtcNow, result, networkPath?.LocalControlAt, networkPath?.LocalControlReady);
        if (result == PhysicalPathState.Changed)
        {
            coreRecovery.RequireFull(TunnelLifecycleReason.PhysicalInterfaceChanged);
            recoveryReason = TunnelLifecycleReason.PhysicalInterfaceChanged;
            return true;
        }
        if (result == PhysicalPathState.Ready) return true;
        // Loss of the Internet interface cannot be repaired by rotating VPN transports or recreating the TUN.
        state = state with { Phase = TunnelPhase.Checking, ObservedAt = DateTimeOffset.UtcNow,
            ProbePassed = false, Code = "OUTBOUND_INTERFACE_UNAVAILABLE", ProtectionActive = protectionActive };
        retryAt = Stopwatch.GetTimestamp() + 5 * Stopwatch.Frequency;
        return false;
    }

    private async Task CheckFrontControlAsync(CancellationToken cancellationToken)
    {
        for (int index = 0; index < 2; index++)
        {
            using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            budget.CancelAfter(TimeSpan.FromSeconds(2));
            bool ready;
            try { _ = await new XrayRouteControl(probe.Port, probe.User, probe.Password).ReadRevisionAsync(budget.Token); ready = true; }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { ready = false; }
            catch (ClientFault) { ready = false; }
            cancellationToken.ThrowIfCancellationRequested();
            if (networkPath is { } path)
                networkPath = path with { LocalControlAt = DateTimeOffset.UtcNow, LocalControlReady = ready };
            if (ready) return;
        }
        coreRecovery.RequireFull(TunnelLifecycleReason.LocalLinkUnavailable);
        throw new ClientFault("FRONT_CONTROL_UNAVAILABLE");
    }
}

internal static class PhysicalPathPolicy
{
    internal static PhysicalPathState Compare(OutboundBinding expected, OutboundBinding? observed)
        => observed is null ? PhysicalPathState.Unavailable : observed == expected ? PhysicalPathState.Ready : PhysicalPathState.Changed;
}
