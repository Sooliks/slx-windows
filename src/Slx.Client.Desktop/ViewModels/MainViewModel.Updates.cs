using Slx.Client.Core;
namespace Slx.Client.Desktop.ViewModels;
public sealed partial class MainViewModel
{
    private bool updateInstalling;
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
            }, () => statusFresh && status?.Phase == TunnelPhase.Disconnected && !status.ProtectionActive && !IsBusy,
            AcquireUpdateInstallAsync, () => !statusFresh ? "UpdateServiceUnconfirmed"
                : status?.Phase != TunnelPhase.Disconnected || status.ProtectionActive ? "UpdateDisconnectFirst"
                : IsBusy ? "UpdateWaitForOperation" : null, persistPreferences);
        Updates.PropertyChanged += (_, args) => { if (args.PropertyName is "" or nameof(UpdateViewModel.InstallerOpened)) NotifyState(); };
    }
    private async Task<IDisposable> AcquireUpdateInstallAsync(CancellationToken cancellationToken)
    {
        CancelStartupConnection();
        await mutationGate.WaitAsync(cancellationToken);
        updateInstalling = true; NotifyState();
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(3));
            var reply = await SendControlAsync(new(PipeContract.Version, "status", Guid.NewGuid()), timeout.Token);
            if (!reply.Success) { MarkStatusUnknown(reply.Code); throw new ClientFault("UPDATE_CONNECTION_ACTIVE"); }
            AcceptStatus(reply.Status);
            if (status.Phase != TunnelPhase.Disconnected || status.ProtectionActive) throw new ClientFault("UPDATE_CONNECTION_ACTIVE");
            return new InstallLease(() => { updateInstalling = false; mutationGate.Release(); NotifyState(); });
        }
        catch (Exception error) when (error is OperationCanceledException or System.IO.IOException or Slx.Client.Core.ClientFault)
        {
            updateInstalling = false; mutationGate.Release(); NotifyState();
            if (cancellationToken.IsCancellationRequested) throw;
            MarkStatusUnknown(error is ClientFault fault ? fault.Code : "SERVICE_TIMEOUT");
            throw new ClientFault("UPDATE_CONNECTION_ACTIVE");
        }
        catch
        {
            updateInstalling = false; mutationGate.Release(); NotifyState(); throw;
        }
    }
    private sealed class InstallLease(Action release) : IDisposable
    {
        private Action? complete = release;
        public void Dispose() => Interlocked.Exchange(ref complete, null)?.Invoke();
    }
}
