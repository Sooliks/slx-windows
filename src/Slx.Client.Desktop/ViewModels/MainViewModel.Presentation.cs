using Slx.Client.Core;
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
}
