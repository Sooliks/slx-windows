using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;
using Slx.Client.Core;
using Slx.Client.Desktop.Localization;
using Slx.Client.Desktop.Presentation;
namespace Slx.Client.Desktop.ViewModels;
public sealed partial class MainViewModel
{
    private bool removeBlocked;
    /// <summary>
    /// Visual tone of the status panel. It only restates facts already shown as text: "connected" requires the same
    /// confirmed observation as <see cref="IsConnected"/>; "unknown" (no fresh observation) is kept apart from "off"
    /// (confirmed disconnected) and "attention" (a reported fault).
    /// </summary>
    public string StatusTone
    {
        get
        {
            if (IsConnected) return "connected";
            if (ServiceStateCopy.IsTrafficFault(LastServiceCode)) return "attention";
            if (!statusFresh) return "unknown";
            if (IsStartupCleanupWait) return "active";
            if (status?.Phase == TunnelPhase.Faulted) return "attention";
            return IsTunnelActive ? "active" : "off";
        }
    }
    /// <summary>Removal waits for a confirmed off state; the dialog explains why instead of silently staying open.</summary>
    public bool RemoveBlocked { get => removeBlocked; private set => SetProperty(ref removeBlocked, value); }
    // One reserved status line carries transient activity, so "Please wait" / "Applying" never inserts a new row.
    // A reported service code keeps priority over activity copy.
    public bool ShowActivityHint => !HasServiceCode && (RulesPending || IsBusy);
    public bool ActivityIsRouting => RulesPending;
    public string ActivityHint => RulesPending ? RoutingUpdateText : Text.Catalog["Busy"];
    private void NotifyActivity()
    {
        OnPropertyChanged(nameof(ShowActivityHint)); OnPropertyChanged(nameof(ActivityIsRouting)); OnPropertyChanged(nameof(ActivityHint));
    }
    // Confirmations float as a toast and clear themselves; warnings and errors stay until dismissed or replaced.
    private static readonly HashSet<string> TransientNotices = ["Imported", "Updated", "DiagnosticsCopied", "UpdateCompleted", "UpdateCancelled"];
    private DispatcherTimer? noticeTimer;
    private RelayCommand? dismissMessage;
    public RelayCommand DismissMessageCommand => dismissMessage ??= new RelayCommand(ClearMessage);
    private void ScheduleNoticeDismiss(string key)
    {
        noticeTimer?.Stop();
        if (disposed || !TransientNotices.Contains(key)) return;
        noticeTimer ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        noticeTimer.Tick -= DismissTransientNotice; noticeTimer.Tick += DismissTransientNotice;
        noticeTimer.Tag = key; noticeTimer.Start();
    }
    private void DismissTransientNotice(object? sender, EventArgs args)
    {
        noticeTimer?.Stop();
        if (!disposed && Equals(noticeTimer?.Tag, messageKey)) ClearMessage();
    }
}
