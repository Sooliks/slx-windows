using System.Diagnostics;
using System.Windows.Threading;
namespace Slx.Client.Desktop.ViewModels;
public sealed partial class UpdateViewModel
{
    private readonly DispatcherTimer countdownTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly Stopwatch countdown = new();
    private bool autoInstallPaused;
    private int countdownSeconds;
    internal Func<TimeSpan>? CountdownElapsedOverride { get; set; }
    public bool IsInstallCountdown => countdown.IsRunning;
    public string CountdownText => Format("UpdateCountdown", countdownSeconds);
    private bool MayInstallAutomatically => !disposed && HasIntegratedUpdate && installAutomatically && prepared != null
        && !busy && !installerOpened && !confirmationVisible && !autoInstallPaused && ShowBanner && canInstall();
    private void RefreshCountdown()
    {
        if (!MayInstallAutomatically) { StopCountdown(); return; }
        if (countdown.IsRunning) return;
        countdownSeconds = 15; countdown.Restart(); countdownTimer.Start();
    }
    private void StopCountdown()
    { countdown.Stop(); countdown.Reset(); countdownTimer.Stop(); countdownSeconds = 0; }
    private async void CountdownTick(object? sender, EventArgs args) => await TickCountdownAsync();
    internal async Task TickCountdownAsync()
    {
        if (!countdown.IsRunning || !MayInstallAutomatically) { StopCountdown(); OnPropertyChanged(""); return; }
        countdownSeconds = Math.Max(0, 15 - (int)(CountdownElapsedOverride?.Invoke() ?? countdown.Elapsed).TotalSeconds);
        OnPropertyChanged(nameof(CountdownText)); OnPropertyChanged(nameof(BannerDescription));
        if (countdownSeconds != 0) return;
        StopCountdown(); await ApplyPreparedAsync(false);
    }
}
