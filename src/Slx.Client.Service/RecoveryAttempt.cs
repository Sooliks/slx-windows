using Slx.Client.Core;

namespace Slx.Client.Service;

internal readonly record struct RecoveryAttemptResult(string? Code)
{
    internal bool Succeeded => Code is null;
}

/// <summary>The selected scope also owns failure cleanup, including failures before a new process starts.</summary>
internal static class RecoveryAttempt
{
    internal static async Task<RecoveryAttemptResult> RunAsync(CoreRecoveryDecision decision, Func<string> verifyRuntime,
        Func<string, CancellationToken, Task> recoverCore, Func<string, CancellationToken, Task> recoverFull,
        Func<string, Task> failCore, Func<string, Task> failFull,
        Action<TunnelLifecycleOutcome, string?> observe, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        observe(TunnelLifecycleOutcome.Started, null);
        try
        {
            string executable = verifyRuntime();
            cancellationToken.ThrowIfCancellationRequested();
            await (decision.Scope == TunnelLifecycleScope.VpnCore ? recoverCore : recoverFull)(executable, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            observe(TunnelLifecycleOutcome.Connected, null);
            return new(null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            observe(TunnelLifecycleOutcome.Cancelled, "OPERATION_INTERRUPTED");
            throw;
        }
        catch (Exception error)
        {
            string code = error is ClientFault fault ? fault.Code : "SERVICE_FAILURE";
            observe(TunnelLifecycleOutcome.Failed, code);
            await (decision.Scope == TunnelLifecycleScope.VpnCore ? failCore : failFull)(code);
            return new(code);
        }
    }
}
