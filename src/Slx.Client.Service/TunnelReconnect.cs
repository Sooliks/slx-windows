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
            await ConnectAsync(plan, sid, repin: true, cancellationToken);
    }

    public async Task RestartAsync(ConnectPlan plan, string sid, CancellationToken cancellationToken)
    {
        var current = currentPlan;
        if (current is not null && ownerSid == sid && ApplicationPlanChange.SameConnection(current, plan)
            && ApplicationPlanChange.SameRoutes(current, plan))
        {
            var fresh = await TunnelEndpoints.PinAsync(current, cancellationToken);
            if (await TryReconnectCoreAsync(current, fresh, cancellationToken)) return;
        }
        await ConnectAsync(plan, sid, repin: true, cancellationToken);
    }

    private async Task<bool> TryReconnectCoreAsync(ConnectPlan plan, ConnectPlan fresh, CancellationToken cancellationToken)
    {
        string executable = RuntimeTrust.Verify();
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
            pinnedPlan = pinned;
            rotation.Reset(pinned);
            try { await RecoverCoreAsync(executable, cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception error)
            {
                string code = error is ClientFault fault ? fault.Code : "CORE_START_FAILED";
                await FailCoreRecoveryAsync(code);
                throw new ClientFault(code);
            }
            retryAt = 0;
            return true;
        }
        finally { lifecycle.Release(); }
    }

    private string OwnerSidOrFault() => ownerSid ?? throw new ClientFault("NO_CONNECTION_PLAN");
}
