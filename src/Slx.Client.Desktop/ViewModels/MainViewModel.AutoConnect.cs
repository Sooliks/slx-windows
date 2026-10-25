using System.Net.NetworkInformation;
using Slx.Client.Core;
namespace Slx.Client.Desktop.ViewModels;
public sealed partial class MainViewModel
{
    private bool startupConnectionInitialized, startupConnectionPending;
    private CancellationTokenSource? startupConnectionOperation;
    internal Func<bool>? StartupNetworkAvailableOverride { get; set; }
    internal void ArmStartupConnection(bool startupLaunch)
    {
        if (startupConnectionInitialized) return;
        startupConnectionInitialized = true;
        startupConnectionPending = startupLaunch && AutoStart && AutoConnectOnStartup && saved != null;
    }
    private void CancelStartupConnection()
    {
        startupConnectionPending = false;
        startupConnectionOperation?.Cancel();
    }
    private async Task TryStartupConnectionAsync()
    {
        if (!startupConnectionPending || disposed) return;
        if (!AutoStart || !AutoConnectOnStartup) { CancelStartupConnection(); return; }
        if (!statusFresh || status == null) return;
        // A known empty service resume store is still finishing its startup off-cleanup.
        if (status.Phase == TunnelPhase.Faulted && !status.ProtectionActive && status.Code == "SERVICE_STARTUP_CLEANUP_PENDING") return;
        // A running/recovering/protected service already has an intent. Never replace it.
        if (status.Phase != TunnelPhase.Disconnected || status.ProtectionActive) { CancelStartupConnection(); return; }
        if (!startupRefreshFinished || IsBusy || polling || UpdateInProgress || HasUpdateMaintenance || HasServiceVersionMismatch) return;
        if (!StartupNetworkAvailable()) return;
        CancelStartupConnection();
        if (saved == null || SelectedProfile == null || IsSelectedMode && !HasSelectedApplications) return;
        // Consume before sending: an unknown acknowledgement must never create another connect request.
        // The service repeats the idle check under its mutation gate to close the observation race.
        await ChangeTunnelAsync("connect-if-idle");
    }
    private bool StartupNetworkAvailable()
    {
        try { return StartupNetworkAvailableOverride?.Invoke() ?? NetworkInterface.GetIsNetworkAvailable(); }
        catch (NetworkInformationException) { Notice("AutoConnectNetworkWait"); return false; }
    }
}
