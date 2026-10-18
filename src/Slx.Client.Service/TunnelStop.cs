using Slx.Client.Core;

namespace Slx.Client.Service;

internal sealed partial class TunnelEngine
{
    private async Task StopChildAsync()
    {
        var process = child;
        // Launch checked that this name was absent before starting our verified child.
        // A successful launch records its GUID; partial launches capture it before stopping.
        if (process is not null && ownedAdapterId is null) ownedAdapterId = LocalProbe.AdapterIdentity();
        child = null;
        job?.Dispose();
        job = null;
        probe = default;
        try
        {
            if (process is not null)
            {
                bool stopped = false;
                try
                {
                    if (!process.HasExited) process.Kill(entireProcessTree: true);
                    using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await process.WaitForExitAsync(budget.Token);
                    stopped = true;
                }
                catch (Exception error) when (error is InvalidOperationException
                    or System.ComponentModel.Win32Exception or OperationCanceledException)
                {
                    stopped = process.HasExited;
                    if (!stopped) { child = process; throw new ClientFault("CORE_STOP_FAILED"); }
                }
                finally { if (stopped) process.Dispose(); }
            }
            // Process exit precedes asynchronous NDIS removal. Observe only our recorded GUID.
            // Never remove an adapter or bypass LaunchAsync's independent name conflict guard.
            await OwnedAdapterCleanup.WaitAsync(ownedAdapterId, LocalProbe.AdapterIdentityExists, CancellationToken.None);
            ownedAdapterId = null;
        }
        catch (Exception error)
        {
            string code = error is ClientFault fault ? fault.Code : "CORE_STOP_FAILED";
            state = new(TunnelPhase.Faulted, DateTimeOffset.UtcNow, state.ProfileName, code, Mode: state.Mode,ProtectionActive:protectionActive);
            if (error is ClientFault) throw;
            throw new ClientFault(code);
        }
    }
}
