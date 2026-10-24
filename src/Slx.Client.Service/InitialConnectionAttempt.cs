using Slx.Client.Core;

namespace Slx.Client.Service;

internal static class InitialConnectionAttempt
{
    internal static bool CanRollback(TunnelStatus status, bool desiredConnected, bool hasPlan, bool hasOwnedResources) =>
        status.Phase is TunnelPhase.Disconnected or TunnelPhase.Faulted
        && !status.ProtectionActive && !desiredConnected && !hasPlan && !hasOwnedResources;

    internal static bool IsDefinitive(Exception error, CancellationToken cancellationToken) =>
        !cancellationToken.IsCancellationRequested && error is ClientFault { Code: not "OPERATION_INTERRUPTED" };

    internal static async Task RunAsync(bool canRollback, Func<Task> connect,
        Func<string, Task> rollback, CancellationToken cancellationToken)
    {
        try { await connect(); }
        catch (Exception error) when (canRollback && IsDefinitive(error, cancellationToken))
        {
            await rollback(((ClientFault)error).Code);
            throw;
        }
    }
}
