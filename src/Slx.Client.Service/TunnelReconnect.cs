using Slx.Client.Core;

namespace Slx.Client.Service;

internal sealed partial class TunnelEngine
{
    /// <summary>
    /// Manual reconnect always resolves the chosen server again and starts from its first transport.
    /// When the TUN process is healthy only the VPN core is replaced, so unselected applications keep running.
    /// </summary>
    public async Task ReconnectAsync(CancellationToken cancellationToken)
    {
        var plan = currentPlan ?? throw new ClientFault("NO_CONNECTION_PLAN");
        string sid = ownerSid ?? throw new ClientFault("NO_CONNECTION_PLAN");
        var fresh = await TunnelEndpoints.PinAsync(plan, cancellationToken);
        if (!await TryReconnectCoreAsync(plan, fresh, cancellationToken))
            await ConnectAsync(plan, sid, repin: true, cancellationToken, TunnelLifecycleOrigin.ReconnectRequest);
    }

    public async Task RestartAsync(ConnectPlan plan, string sid, CancellationToken cancellationToken)
    {
        var current = currentPlan;
        if (current is not null && ownerSid == sid && ApplicationPlanChange.SameConnection(current, plan)
            && ApplicationPlanChange.SameRoutes(current, plan))
        {
            // The UI's plan carries the subscription's current alternates (an upgraded intent may have none).
            var desired = current with { Alternates = plan.Alternates };
            var fresh = await TunnelEndpoints.PinAsync(desired, cancellationToken);
            if (await TryReconnectCoreAsync(current, fresh, cancellationToken, desired)) return;
        }
        await ConnectAsync(plan, sid, repin: true, cancellationToken, TunnelLifecycleOrigin.ReconnectRequest);
    }

    /// <summary>
    /// An already running connection (for example restored from an older version's intent) adopts the other
    /// transports of the same server from the UI's plan, so later recovery can switch transport. Best effort.
    /// </summary>
    public async Task AdoptAlternatesAsync(ConnectPlan plan, string sid, CancellationToken cancellationToken)
    {
        try
        {
            var current = currentPlan;
            if (plan.Alternates is not { Count: > 0 } || current is null || ownerSid != sid
                || !ApplicationPlanChange.SameConnection(current, plan) || pinnedPlan is not { } before
                || TunnelEndpoints.HasAlternates(before, plan)) return;
            PlanValidator.Validate(plan);
            var pinned = await TunnelEndpoints.PinAsync(current with { Alternates = plan.Alternates }, cancellationToken, before);
            await lifecycle.WaitAsync(cancellationToken);
            try
            {
                if (!ReferenceEquals(currentPlan, current) || !ReferenceEquals(pinnedPlan, before) || !desiredConnected) return;
                var next = before with { Alternates = pinned.Alternates };
                intents.Save(new(OwnerSidOrFault(), next, current.Profile.Host));
                currentPlan = current with { Alternates = plan.Alternates };
                pinnedPlan = next;
                rotation.Ensure(next);
                PublishAttempt();
            }
            finally { lifecycle.Release(); }
        }
        catch (Exception error) when (error is ClientFault or OperationCanceledException or IOException
            or UnauthorizedAccessException or System.Security.Cryptography.CryptographicException) { }
    }

    private async Task<bool> TryReconnectCoreAsync(ConnectPlan plan, ConnectPlan fresh, CancellationToken cancellationToken,
        ConnectPlan? desired = null)
    {
        await lifecycle.WaitAsync(cancellationToken);
        try
        {
            using var work = connectionWork.Begin(cancellationToken);
            cancellationToken = work.Token;
            cancellationToken.ThrowIfCancellationRequested();
            var before = pinnedPlan;
            if (offCleanup.Pending || !desiredConnected || !ReferenceEquals(currentPlan, plan) || before is null
                || pendingApplicationUpdate is not null || !CanRecoverCoreOnly()) return false;
            // Whole-computer mode sends unknown destinations to the VPN; keep the TUN process's direct
            // endpoint list valid by rebuilding both processes when the server moved to a new address.
            var known = TunnelEndpoints.Hosts(before).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (plan.Mode == RoutingMode.WholeComputer && !TunnelEndpoints.Hosts(fresh).All(known.Contains)) return false;
            var pinned = fresh with { Applications = before.Applications };
            intents.Save(new(OwnerSidOrFault(), pinned, plan.Profile.Host));
            if (desired is not null) currentPlan = desired;
            pinnedPlan = pinned;
            rotation.Reset(pinned);
            var result = await RunRecoveryAsync(TunnelLifecycleOrigin.ReconnectRequest,
                new(TunnelLifecycleScope.VpnCore, TunnelLifecycleReason.Requested), cancellationToken);
            if (!result.Succeeded) throw new ClientFault(result.Code!);
            return true;
        }
        finally { lifecycle.Release(); }
    }

    private string OwnerSidOrFault() => ownerSid ?? throw new ClientFault("NO_CONNECTION_PLAN");
}
