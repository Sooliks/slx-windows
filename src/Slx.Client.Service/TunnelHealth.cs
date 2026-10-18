using Slx.Client.Core;

namespace Slx.Client.Service;

internal sealed partial class TunnelEngine
{
    public async Task CheckHealthAsync(CancellationToken cancellationToken)
    {
        await lifecycle.WaitAsync(cancellationToken);
        try
        {
            if (state.Phase != TunnelPhase.Connected || child is null || currentPlan is null) return;
            try
            {
                InteractiveSessions.RequireSingleOwner(ownerSid ?? "");
                if (child.HasExited) throw new ClientFault("CORE_EXITED");
                if (!LocalProbe.TunnelReady()) throw new ClientFault("TUN_NOT_READY");
                if (await LocalProbe.CheckOnceAsync(probe.Port, probe.User, probe.Password, cancellationToken))
                {
                    state = state with { ObservedAt = DateTimeOffset.UtcNow, ProbePassed = true };
                    return;
                }
                state = state with { Phase = TunnelPhase.Reconnecting, ObservedAt = DateTimeOffset.UtcNow, ProbePassed = false };
                await StopChildAsync();
                if (recoveryAttempts++ >= 1) throw new ClientFault("RECOVERY_LIMIT");
                var pinned = await PinEndpointAsync(currentPlan, cancellationToken);
                await LaunchAsync(pinned, RuntimeTrust.Verify(), TunnelPhase.Reconnecting, cancellationToken);
            }
            catch (Exception error)
            {
                await StopChildAsync();
                state = new(TunnelPhase.Faulted, DateTimeOffset.UtcNow, currentPlan.Profile.Name,
                    error is ClientFault fault ? fault.Code : error is OperationCanceledException ? "OPERATION_INTERRUPTED" : "SERVICE_FAILURE", Mode: currentPlan.Mode);
            }
        }
        finally { lifecycle.Release(); }
    }
}
