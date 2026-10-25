using System.ComponentModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Slx.Client.Core;
using Slx.Client.Desktop.Localization;
using Slx.Client.Windows;
namespace Slx.Client.Desktop.ViewModels;
public sealed partial class UpdateViewModel : ObservableObject, IDisposable
{
    private readonly ReleaseUpdateClient client = new();
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromHours(6) };
    private readonly CancellationTokenSource lifetime = new();
    private readonly Action<bool, DateTimeOffset?> save;
    private readonly Func<bool> canInstall;
    private readonly Func<CancellationToken, Task<IDisposable>> beginInstall;
    private readonly Func<string?>? installBlockReason;
    private readonly Func<PreparedUpdate, bool, IProgress<UpdateApplyProgress>, CancellationToken, Task<UpdateApplyResult>>? integratedApply;
    private readonly Func<bool>? connectionActive;
    private readonly Action<bool>? saveInstallAutomatically;
    private readonly string version;
    private readonly bool reuseStaged;
    private bool automatic, busy, disposed, started, installerOpened, installAutomatically, confirmationVisible;
    private DateTimeOffset? remindAfter;
    private UpdateOffer? offer;
    private PreparedUpdate? prepared;
    private string stateKey = "UpdateNotChecked";
    private string? checkWarningKey;
    private int progress, downloadGeneration, applyGeneration;
    internal Func<string, CancellationToken, Task<UpdateCheckResult>>? CheckOverride { get; set; }
    internal Func<UpdateOffer, IProgress<int>?, CancellationToken, Task<PreparedUpdate>>? DownloadOverride { get; set; }
    internal Func<PreparedUpdate, CancellationToken, Task<UpdateLaunchResult>>? LaunchOverride { get; set; }
    internal Func<UpdateOffer, CancellationToken, Task<PreparedUpdate?>>? FindPreparedOverride { get; set; }
    public UpdateViewModel(string version, bool automatic, DateTimeOffset? reminder,
        Action<bool, DateTimeOffset?> save, Func<bool> canInstall, Func<CancellationToken, Task<IDisposable>> beginInstall,
        Func<string?>? installBlockReason = null, bool reuseStaged = false,
        Func<PreparedUpdate, bool, IProgress<UpdateApplyProgress>, CancellationToken, Task<UpdateApplyResult>>? integratedApply = null,
        Func<bool>? connectionActive = null, bool installAutomatically = false, Action<bool>? saveInstallAutomatically = null)
    {
        this.version = version; this.automatic = automatic; remindAfter = reminder; this.save = save; this.canInstall = canInstall;
        this.beginInstall = beginInstall; this.installBlockReason = installBlockReason; this.reuseStaged = reuseStaged;
        this.integratedApply = integratedApply; this.connectionActive = connectionActive;
        this.installAutomatically = installAutomatically; this.saveInstallAutomatically = saveInstallAutomatically;
        CheckCommand = new AsyncRelayCommand(() => CheckAsync(true), () => !busy && !installerOpened);
        ApplyCommand = new AsyncRelayCommand(ApplyAsync, CanApply);
        LaterCommand = new RelayCommand(RemindTomorrow, () => !busy || IsDownloading);
        ConfirmUpdateCommand = new AsyncRelayCommand(() => ApplyPreparedAsync(true), () => confirmationVisible && !busy && connectionActive?.Invoke() == true);
        CancelConfirmationCommand = new RelayCommand(CancelConfirmation);
        timer.Tick += AutomaticTick; countdownTimer.Tick += CountdownTick;
        Text.Catalog.PropertyChanged += LanguageChanged;
    }
    public AsyncRelayCommand CheckCommand { get; }
    public AsyncRelayCommand ApplyCommand { get; }
    public AsyncRelayCommand ConfirmUpdateCommand { get; }
    public RelayCommand CancelConfirmationCommand { get; }
    public RelayCommand LaterCommand { get; }
    public bool Automatic
    {
        get => automatic;
        set { if (SetProperty(ref automatic, value)) { save(value, remindAfter); if (started && value) _ = CheckAsync(false); } }
    }
    public bool InstallAutomatically
    {
        get => installAutomatically;
        set { if (SetProperty(ref installAutomatically, value)) { autoInstallPaused = false; saveInstallAutomatically?.Invoke(value); Changed(); } }
    }
    public bool HasIntegratedUpdate => integratedApply != null;
    public bool ShowBanner => offer != null && (!remindAfter.HasValue || remindAfter <= DateTimeOffset.UtcNow);
    public bool IsBusy => busy;
    public bool InstallerOpened => installerOpened;
    public bool IsDownloading => busy && stateKey == "UpdateDownloading";
    public bool IsApplying => busy && !IsDownloading && stateKey != "UpdateChecking";
    public bool ConfirmationVisible => confirmationVisible;
    public bool ShowNormalActions => !confirmationVisible;
    public int Progress => progress;
    public string Status => Text.Catalog[stateKey];
    public string CurrentVersion => version;
    public string CheckWarning => checkWarningKey == null ? "" : Text.Catalog[checkWarningKey];
    public bool HasCheckWarning => checkWarningKey != null;
    public void Start()
    {
        if (started || disposed) return;
        started = true; timer.Start(); _ = CheckAsync(false);
    }
    private async void AutomaticTick(object? sender, EventArgs args) => await CheckAsync(false);
    internal async Task CheckAsync(bool manual)
    {
        if (disposed || busy || installerOpened || !manual && (!automatic || remindAfter > DateTimeOffset.UtcNow)) return;
        if (manual) { remindAfter = null; autoInstallPaused = false; save(automatic, null); }
        confirmationVisible = false;
        SetBusy(true, "UpdateChecking");
        bool download = false;
        try
        {
            var result = await (CheckOverride?.Invoke(version, lifetime.Token) ?? client.CheckAsync(version, lifetime.Token));
            if (disposed) return;
            checkWarningKey = null;
            if (result.State == UpdateCheckState.Available && result.Offer != null)
            {
                if (offer != result.Offer) { prepared = null; installerOpened = false; autoInstallPaused = false; }
                offer = result.Offer;
                if (prepared == null && (reuseStaged || FindPreparedOverride != null))
                    prepared = await (FindPreparedOverride?.Invoke(offer, lifetime.Token) ?? client.FindPreparedAsync(offer, lifetime.Token));
                if (disposed) return;
                stateKey = prepared == null ? "UpdateFound" : "UpdateReady";
                download = HasIntegratedUpdate && automatic && prepared == null;
            }
            else if (result.State == UpdateCheckState.Current) { offer = null; prepared = null; stateKey = "UpdateCurrent"; }
            else SetCheckFailure(result.ErrorCode);
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (ClientFault fault) { SetCheckFailure(fault.Code); }
        finally { SetBusy(false); }
        if (download && !disposed) await DownloadAsync();
    }
    private void SetCheckFailure(string? code)
    {
        var key = CheckFailureKey(code);
        if (HasIntegratedUpdate && prepared != null) { checkWarningKey = key; stateKey = "UpdateReady"; }
        else stateKey = key;
    }
    private void RemindTomorrow()
    {
        CancelConfirmation(); StopCountdown();
        remindAfter = DateTimeOffset.UtcNow.AddDays(1); save(automatic, remindAfter); Changed();
    }
    private void SetBusy(bool value, string? key = null)
    {
        busy = value; if (key != null) stateKey = key; Changed();
    }
    public void RefreshInstallAvailability()
    {
        if (confirmationVisible && connectionActive?.Invoke() != true) confirmationVisible = false;
        RefreshCountdown();
        foreach (var property in new[] { nameof(InstallHint), nameof(BannerDescription), nameof(IsInstallBlocked), nameof(ShowInstallHint),
            nameof(ConfirmationVisible), nameof(ShowNormalActions), nameof(IsInstallCountdown), nameof(CountdownText) }) OnPropertyChanged(property);
        ApplyCommand.NotifyCanExecuteChanged(); ConfirmUpdateCommand.NotifyCanExecuteChanged();
    }
    private void Changed()
    {
        RefreshCountdown(); OnPropertyChanged("");
        CheckCommand.NotifyCanExecuteChanged(); ApplyCommand.NotifyCanExecuteChanged(); LaterCommand.NotifyCanExecuteChanged();
        ConfirmUpdateCommand.NotifyCanExecuteChanged();
    }
    private void LanguageChanged(object? sender, PropertyChangedEventArgs args) => Changed();
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; lifetime.Cancel(); timer.Stop(); timer.Tick -= AutomaticTick;
        countdownTimer.Stop(); countdownTimer.Tick -= CountdownTick;
        Text.Catalog.PropertyChanged -= LanguageChanged; lifetime.Dispose();
    }
}
