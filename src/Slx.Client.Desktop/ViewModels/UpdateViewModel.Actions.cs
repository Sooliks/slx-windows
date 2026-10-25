using Slx.Client.Core;
using Slx.Client.Windows;
namespace Slx.Client.Desktop.ViewModels;
public sealed partial class UpdateViewModel
{
    private bool CanApply() => !busy && !installerOpened && !confirmationVisible && offer != null
        && (prepared == null || canInstall() || HasIntegratedUpdate && connectionActive?.Invoke() == true);
    private async Task ApplyAsync()
    {
        if (disposed || !CanApply()) return;
        StopCountdown();
        if (prepared == null) { await DownloadAsync(); return; }
        autoInstallPaused = true;
        if (HasIntegratedUpdate && connectionActive?.Invoke() == true)
        { confirmationVisible = true; Changed(); return; }
        await ApplyPreparedAsync(false);
    }
    private async Task DownloadAsync()
    {
        if (disposed || busy || installerOpened || offer == null || prepared != null) return;
        progress = 0; SetBusy(true, "UpdateDownloading");
        var generation = ++downloadGeneration;
        try
        {
            var reporter = new Progress<int>(value =>
            {
                if (disposed || generation != downloadGeneration || !IsDownloading) return;
                progress = Math.Clamp(value, 0, 100);
                OnPropertyChanged(nameof(Progress)); OnPropertyChanged(nameof(ProgressText));
            });
            var downloaded = await (DownloadOverride?.Invoke(offer, reporter, lifetime.Token) ?? client.DownloadAsync(offer, reporter, lifetime.Token));
            if (disposed) return;
            prepared = downloaded; stateKey = "UpdateReady";
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (ClientFault fault)
        { stateKey = fault.Code == "UPDATE_INTEGRITY_FAILED" ? "UpdateIntegrityFailed" : "UpdateActionFailed"; }
        finally { SetBusy(false); }
    }
    private async Task ApplyPreparedAsync(bool resume)
    {
        if (disposed || busy || installerOpened || prepared == null) return;
        if (resume ? !confirmationVisible || connectionActive?.Invoke() != true : !canInstall())
        { confirmationVisible = false; Changed(); return; }
        confirmationVisible = false; StopCountdown(); autoInstallPaused = true;
        SetBusy(true, HasIntegratedUpdate ? "UpdatePhasePreparing" : "UpdateStarting");
        try
        {
            if (integratedApply != null)
            {
                var generation = ++applyGeneration;
                var reporter = new Progress<UpdateApplyProgress>(value =>
                {
                    if (disposed || generation != applyGeneration || !IsApplying) return;
                    stateKey = value.Phase switch
                    {
                        "authorizing" => "UpdatePhaseAuthorizing", "installing" => "UpdatePhaseInstalling",
                        "verifying" => "UpdatePhaseVerifying", "restarting" => "UpdatePhaseRestarting",
                        _ => "UpdatePhasePreparing"
                    };
                    OnPropertyChanged(nameof(Status)); OnPropertyChanged(nameof(BannerDescription));
                });
                var result = await integratedApply(prepared, resume, reporter, lifetime.Token);
                if (disposed) return;
                installerOpened = result == UpdateApplyResult.Restarting;
                stateKey = result switch
                {
                    UpdateApplyResult.Restarting => "UpdatePhaseRestarting", UpdateApplyResult.Cancelled => "UpdateInstallCancelled",
                    UpdateApplyResult.RebootRequired => "UpdateRebootRequired", _ => "UpdateIntegratedFailed"
                };
            }
            else
            {
                using var lease = await beginInstall(lifetime.Token);
                if (disposed) return;
                lifetime.Token.ThrowIfCancellationRequested();
                var result = await (LaunchOverride?.Invoke(prepared, lifetime.Token) ?? client.LaunchInstallerAsync(prepared, lifetime.Token));
                installerOpened = result == UpdateLaunchResult.Started;
                stateKey = installerOpened ? "UpdateInstallerOpened" : "UpdateInstallCancelled";
            }
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (ClientFault fault)
        {
            stateKey = fault.Code switch
            {
                "UPDATE_INTEGRITY_FAILED" => "UpdateIntegrityFailed", "UPDATE_CONNECTION_ACTIVE" => "UpdateDisconnectFirst",
                _ => HasIntegratedUpdate ? "UpdateIntegratedFailed" : "UpdateActionFailed"
            };
            if (fault.Code == "UPDATE_INTEGRITY_FAILED" || !HasIntegratedUpdate && (fault.Code is "UPDATE_INSTALL_FAILED" or "UPDATE_DOWNLOAD_FAILED")) prepared = null;
        }
        finally { SetBusy(false); }
    }
    private void CancelConfirmation()
    { confirmationVisible = false; autoInstallPaused = true; Changed(); }
}
