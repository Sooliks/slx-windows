using Slx.Client.Core;

namespace Slx.Client.Service;

/// <summary>Restoring remote HTTPS is insufficient: the preserved local routes must still belong to this TUN.</summary>
internal static class CoreRecoveryAttempt
{
    internal static TunnelStatus ConnectedStatus(ConnectPlan plan, bool protectionActive, bool applicationUpdatePending)
    {
        var at = DateTimeOffset.UtcNow;
        return new(TunnelPhase.Connected, at, plan.Profile.Name,
            Code: applicationUpdatePending ? "ROUTING_UPDATE_UNCONFIRMED" : null, ConnectedAt: at,
            Mode: plan.Mode, ProbePassed: true, ProtectionActive: protectionActive,
            ApplicationRevision: applicationUpdatePending ? null : XrayRouteRevision.For(plan));
    }

    internal static async Task RunAsync(Func<Task> stopCore, Func<CancellationToken, Task> startCore,
        Func<CancellationToken, Task> waitForVpn, Func<CancellationToken, Task> checkRoutes,
        Action routesFailed, Func<bool> resourcesReady, Action connected, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await stopCore();
        cancellationToken.ThrowIfCancellationRequested();
        await startCore(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        await waitForVpn(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        try { await checkRoutes(cancellationToken); }
        catch (ClientFault fault) when (fault.Code == TrafficRouteProbe.FailureCode)
        {
            cancellationToken.ThrowIfCancellationRequested();
            routesFailed();
            throw;
        }
        cancellationToken.ThrowIfCancellationRequested();
        bool ready = resourcesReady();
        cancellationToken.ThrowIfCancellationRequested();
        if (!ready) throw new ClientFault("CORE_EXITED");
        connected();
    }
}
