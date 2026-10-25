using Slx.Client.Core;
namespace Slx.Client.Desktop.ViewModels;
public sealed partial class MainViewModel
{
    private bool updateInstalling;
    private bool HasUpdateMaintenance => statusFresh && (status?.UpdateMaintenance is not null
        || status?.Code == "UPDATE_MAINTENANCE_UNCONFIRMED");
    private bool HasServiceVersionMismatch => persistPreferences && statusFresh && status is not null
        && status.ServiceVersion != VersionLabel["SLX VPN · ".Length..];
    private bool UpdateInProgress => updateInstalling || Updates?.InstallerOpened == true;
    public UpdateViewModel Updates { get; private set; } = null!;
    private void InitializeUpdates()
    {
        var version = VersionLabel["SLX VPN · ".Length..];
        Updates = new(version, preferences.CheckUpdatesAutomatically, preferences.UpdateReminderUntil,
            (automatic, reminder) =>
            {
                preferences = preferences with { CheckUpdatesAutomatically = automatic, UpdateReminderUntil = reminder };
                SavePreferences();
            }, () => statusFresh && status?.Phase == TunnelPhase.Disconnected && !status.ProtectionActive && !IsBusy && !IsOverlayVisible
                && !UpdateInProgress && !HasUpdateMaintenance && !HasServiceVersionMismatch,
            AcquireUpdateInstallAsync, () => !statusFresh ? "UpdateServiceUnconfirmed"
                : HasUpdateMaintenance ? "UpdateMaintenanceWait"
                : status?.Phase != TunnelPhase.Disconnected || status.ProtectionActive ? "UpdateDisconnectFirst"
                : IsBusy ? "UpdateWaitForOperation" : null, persistPreferences,
            persistPreferences ? ApplyIntegratedUpdateAsync : null, () => statusFresh && IsTunnelActive,
            preferences.InstallUpdatesAutomatically, automatic =>
            {
                preferences = preferences with { InstallUpdatesAutomatically = automatic };
                SavePreferences();
            });
        Updates.PropertyChanged += (_, args) => { if (args.PropertyName is "" or nameof(UpdateViewModel.InstallerOpened)) NotifyState(); };
    }
    private async Task<IDisposable> AcquireUpdateInstallAsync(CancellationToken cancellationToken)
    {
        CancelStartupConnection();
        await mutationGate.WaitAsync(cancellationToken);
        updateInstalling = true; NotifyState();
        bool granted = false;
        var owner = generation;
        var revision = observationRevision;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(3));
            PipeReply reply;
            try
            {
                reply = await SendControlAsync(new(PipeContract.Version, "status", Guid.NewGuid()), timeout.Token).WaitAsync(timeout.Token);
            }
            catch (Exception error) when (error is OperationCanceledException or System.IO.IOException or ClientFault)
            {
                if (!disposed && owner == generation && revision == observationRevision)
                    MarkStatusUnknown(error is ClientFault fault ? fault.Code : "SERVICE_TIMEOUT");
                cancellationToken.ThrowIfCancellationRequested();
                throw new ClientFault("UPDATE_CONNECTION_ACTIVE");
            }
            cancellationToken.ThrowIfCancellationRequested();
            // Never overwrite a newer read with this snapshot; a newer confirmed off can still admit installation.
            if (disposed || owner != generation)
                throw new ClientFault("UPDATE_CONNECTION_ACTIVE");
            if (!reply.Success)
            {
                if (revision == observationRevision) MarkStatusUnknown(reply.Code);
                throw new ClientFault("UPDATE_CONNECTION_ACTIVE");
            }
            if (revision == observationRevision) AcceptStatus(reply.Status);
            if (!statusFresh || status?.Phase != TunnelPhase.Disconnected || status.ProtectionActive)
                throw new ClientFault("UPDATE_CONNECTION_ACTIVE");
            granted = true;
            return new InstallLease(() => { updateInstalling = false; mutationGate.Release(); NotifyState(); });
        }
        finally
        {
            if (!granted) { updateInstalling = false; mutationGate.Release(); NotifyState(); }
        }
    }
    private sealed class InstallLease(Action release) : IDisposable
    {
        private Action? complete = release;
        public void Dispose() => Interlocked.Exchange(ref complete, null)?.Invoke();
    }
}
