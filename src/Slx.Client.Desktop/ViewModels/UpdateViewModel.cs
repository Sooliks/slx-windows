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
    private readonly string version;
    private readonly bool reuseStaged;
    private bool automatic, busy, disposed, started, installerOpened;
    private DateTimeOffset? remindAfter;
    private UpdateOffer? offer;
    private PreparedUpdate? prepared;
    private string stateKey = "UpdateNotChecked";
    private int progress, downloadGeneration;
    internal Func<string, CancellationToken, Task<UpdateCheckResult>>? CheckOverride { get; set; }
    internal Func<UpdateOffer, IProgress<int>?, CancellationToken, Task<PreparedUpdate>>? DownloadOverride { get; set; }
    internal Func<PreparedUpdate, CancellationToken, Task<UpdateLaunchResult>>? LaunchOverride { get; set; }
    internal Func<UpdateOffer, CancellationToken, Task<PreparedUpdate?>>? FindPreparedOverride { get; set; }
    public UpdateViewModel(string version, bool automatic, DateTimeOffset? reminder,
        Action<bool, DateTimeOffset?> save, Func<bool> canInstall, Func<CancellationToken, Task<IDisposable>> beginInstall,
        Func<string?>? installBlockReason = null, bool reuseStaged = false)
    {
        this.version = version; this.automatic = automatic; remindAfter = reminder; this.save = save; this.canInstall = canInstall; this.beginInstall = beginInstall;
        this.installBlockReason = installBlockReason; this.reuseStaged = reuseStaged;
        CheckCommand = new AsyncRelayCommand(() => CheckAsync(true), () => !busy);
        ApplyCommand = new AsyncRelayCommand(ApplyAsync, () => !busy && !installerOpened && offer != null && (prepared == null || canInstall()));
        LaterCommand = new RelayCommand(RemindTomorrow, () => !busy || IsDownloading);
        timer.Tick += AutomaticTick;
        Text.Catalog.PropertyChanged += LanguageChanged;
    }
    public AsyncRelayCommand CheckCommand { get; }
    public AsyncRelayCommand ApplyCommand { get; }
    public RelayCommand LaterCommand { get; }
    public bool Automatic
    {
        get => automatic;
        set { if (SetProperty(ref automatic, value)) { save(value, remindAfter); if (started && value) _ = CheckAsync(false); } }
    }
    public bool ShowBanner => offer != null && (!remindAfter.HasValue || remindAfter <= DateTimeOffset.UtcNow);
    public bool IsBusy => busy;
    public bool InstallerOpened => installerOpened;
    public bool IsDownloading => busy && stateKey == "UpdateDownloading";
    public int Progress => progress;
    public string Status => Text.Catalog[stateKey];
    public string CurrentVersion => version;
    public void Start()
    {
        if (started || disposed) return;
        started = true; timer.Start(); _ = CheckAsync(false);
    }
    private async void AutomaticTick(object? sender, EventArgs args) => await CheckAsync(false);
    internal async Task CheckAsync(bool manual)
    {
        if (disposed || busy || !manual && (!automatic || remindAfter > DateTimeOffset.UtcNow)) return;
        if (manual) { remindAfter = null; save(automatic, null); }
        SetBusy(true, "UpdateChecking");
        try
        {
            var result = await (CheckOverride?.Invoke(version, lifetime.Token) ?? client.CheckAsync(version, lifetime.Token));
            if (disposed) return;
            if (result.State == UpdateCheckState.Available && result.Offer != null)
            {
                if (offer != result.Offer) { prepared = null; installerOpened = false; }
                offer = result.Offer;
                // An installer for this exact release that is already staged and verified is offered for install directly.
                // Render checks and UI fixtures never read the real staging folder.
                if (prepared == null && (reuseStaged || FindPreparedOverride != null))
                    prepared = await (FindPreparedOverride?.Invoke(offer, lifetime.Token) ?? client.FindPreparedAsync(offer, lifetime.Token));
                if (disposed) return;
                stateKey = prepared == null ? "UpdateFound" : "UpdateReady";
            }
            else if (result.State == UpdateCheckState.Current) { offer = null; prepared = null; stateKey = "UpdateCurrent"; }
            else stateKey = "UpdateCheckFailed";
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (ClientFault) { stateKey = "UpdateCheckFailed"; }
        finally { SetBusy(false); }
    }
    private async Task ApplyAsync()
    {
        if (disposed || busy || installerOpened || offer == null) return;
        if (prepared != null && !canInstall()) { Changed(); return; }
        if (prepared == null) progress = 0;
        SetBusy(true, prepared == null ? "UpdateDownloading" : "UpdateStarting");
        try
        {
            if (prepared == null)
            {
                var generation = ++downloadGeneration;
                var reporter = new Progress<int>(value =>
                {
                    if (disposed || generation != downloadGeneration || !IsDownloading) return;
                    progress = Math.Clamp(value, 0, 100);
                    OnPropertyChanged(nameof(Progress)); OnPropertyChanged(nameof(ProgressText));
                });
                prepared = await (DownloadOverride?.Invoke(offer, reporter, lifetime.Token) ?? client.DownloadAsync(offer, reporter, lifetime.Token));
                stateKey = "UpdateReady";
            }
            else
            {
                // Check the current service observation immediately before the explicit installation handoff.
                if (!canInstall()) { stateKey = "UpdateReady"; return; }
                using var lease = await beginInstall(lifetime.Token);
                if (disposed) return;
                lifetime.Token.ThrowIfCancellationRequested();
                var result = await (LaunchOverride?.Invoke(prepared, lifetime.Token) ?? client.LaunchInstallerAsync(prepared, lifetime.Token));
                installerOpened = result == UpdateLaunchResult.Started;
                stateKey = result == UpdateLaunchResult.Started ? "UpdateInstallerOpened" : "UpdateInstallCancelled";
            }
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (ClientFault fault)
        {
            stateKey = fault.Code switch { "UPDATE_INTEGRITY_FAILED" => "UpdateIntegrityFailed",
                "UPDATE_CONNECTION_ACTIVE" => "UpdateDisconnectFirst", _ => "UpdateActionFailed" };
            if (fault.Code is "UPDATE_INTEGRITY_FAILED" or "UPDATE_INSTALL_FAILED" or "UPDATE_DOWNLOAD_FAILED") prepared = null;
        }
        finally { SetBusy(false); }
    }
    private void RemindTomorrow()
    {
        remindAfter = DateTimeOffset.UtcNow.AddDays(1); save(automatic, remindAfter); Changed();
    }
    private void SetBusy(bool value, string? key = null)
    {
        busy = value; if (key != null) stateKey = key; Changed();
    }
    public void RefreshInstallAvailability()
    {
        OnPropertyChanged(nameof(InstallHint)); OnPropertyChanged(nameof(BannerDescription));
        OnPropertyChanged(nameof(IsInstallBlocked)); OnPropertyChanged(nameof(ShowInstallHint)); ApplyCommand.NotifyCanExecuteChanged();
    }
    private void Changed()
    {
        OnPropertyChanged(""); CheckCommand.NotifyCanExecuteChanged(); ApplyCommand.NotifyCanExecuteChanged(); LaterCommand.NotifyCanExecuteChanged();
    }
    private void LanguageChanged(object? sender, PropertyChangedEventArgs args) => Changed();
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; lifetime.Cancel(); timer.Stop(); timer.Tick -= AutomaticTick;
        Text.Catalog.PropertyChanged -= LanguageChanged; lifetime.Dispose();
    }
}
