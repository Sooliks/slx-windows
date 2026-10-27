using Slx.Client.Core;
using Slx.Client.Windows;

namespace Slx.Client.Service;

internal sealed partial class TunnelEngine
{
    private readonly PendingOffCleanup offCleanup = new();

    private void RequestOff(string? completionCode = null)
    {
        remoteHealthGrace.Reset();
        startupOffCleanup.Cancel();
        desiredConnected = false;
        offCleanup.Begin(completionCode);
    }

    private async Task<bool> ContinueOffCleanupAsync(bool force = false)
    {
        if (!offCleanup.Pending) return false;
        try
        {
            if (await offCleanup.TryAsync(DisconnectOwnedAsync, force))
            {
                state = new(TunnelPhase.Disconnected, DateTimeOffset.UtcNow, Code: offCleanup.CompletionCode);
                ownerSid = null;
                confirmedUnprotected = true;
            }
        }
        catch (Exception error)
        {
            string code = error is ClientFault fault ? fault.Code : "PROTECTION_STORAGE_FAILED";
            state = new(TunnelPhase.Faulted, DateTimeOffset.UtcNow, state.ProfileName, code,
                Mode: state.Mode, ProtectionActive: protectionActive);
            if (force) throw new ClientFault(code);
        }
        return true;
    }

    private async Task DisconnectOwnedAsync()
    {
        // Persist off before changing network policy, and remove blocks only after our core/adapter
        // are gone. Any failed step retains the off retry; no reconnect is allowed to replace it.
        intents.Save(null);
        state = state with { Phase = TunnelPhase.Stopping, ObservedAt = DateTimeOffset.UtcNow, ProbePassed = false };
        await StopChildAsync();
        ReleaseProtection();
        pendingApplicationUpdate = null;
        currentPlan = pinnedPlan = null;
        DnsCache.Refresh();
    }
}
