using System.IO;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using Slx.Client.Core;
using Slx.Client.Desktop.Localization;
using Slx.Client.Desktop.Presentation;
namespace Slx.Client.Desktop.ViewModels;
public sealed partial class MainViewModel
{
    private readonly SemaphoreSlim mutationGate = new(1, 1);
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(3) };
    private CancellationTokenSource? operation;
    private long generation;
    private bool polling, disposed;
    public void Initialize(bool startupLaunch = false)
    {
        if (persistPreferences) ArmStartupConnection(startupLaunch);
        ReconcileStartup();
        timer.Tick += PollTick;
        timer.Start();
        _ = PollAsync();
        _ = RefreshOnStartupAsync();
        _ = WarmDiscoveryCacheAsync();
        Updates.Start();
    }
    private async void PollTick(object? sender, EventArgs e) => await PollAsync();
    internal async Task PollAsync()
    {
        if (polling || IsBusy || disposed) return;
        polling = true;
        var owner = generation;
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var reply = await SendControlAsync(new(PipeContract.Version, "status", Guid.NewGuid()), timeout.Token);
            if (owner != generation || disposed) return;
            if (!reply.Success) { MarkStatusUnknown(reply.Code); ShowFault(reply.Code); return; }
            status = reply.Status;
            statusFresh = true;
            ReconcileApplicationRevision();
            if (!ServiceStateCopy.IsTrafficFault(lastServiceCode) || status.Phase == TunnelPhase.Connected && status.ProbePassed) lastServiceCode = null;
            if (status.Phase == TunnelPhase.Faulted && status.Code != null) ShowFault(status.Code);
        }
        catch (Exception error) when (error is ClientFault or OperationCanceledException or IOException)
        { if (owner == generation) MarkStatusUnknown(error is ClientFault fault ? fault.Code : "SERVICE_TIMEOUT"); }
        finally { polling = false; NotifyState(); }
        ResumeDeferredRouting();
        await TryStartupConnectionAsync();
    }
    private Task ToggleAsync() => IsTunnelActive || IsBusy ? ChangeTunnelAsync("disconnect") : ChangeTunnelAsync("connect");
    private async Task ChangeTunnelAsync(string action)
    {
        if (action != "connect-if-idle") CancelStartupConnection();
        if (UpdateInProgress) return;
        var routingOwner = BeginRoutingMutation();
        if (action != "disconnect" && saved == null) { OpenImportCommand.Execute(null); return; }
        if (action != "disconnect" && IsSelectedMode && !HasSelectedApplications) { Notice("NoActiveApps"); Page = "applications"; return; }
        ConnectPlan? plan = null;
        var owner = ++generation;
        operation?.Cancel();
        using var current = new CancellationTokenSource(TimeSpan.FromSeconds(55));
        operation = current;
        if (action == "connect-if-idle") startupConnectionOperation = current;
        IsBusy = true;
        ClearMessage();
        var entered = false;
        try
        {
            if (action != "disconnect") await RepairVersionedApplicationsAsync(current.Token);
            if(action != "disconnect" && SelectedProfile != null)plan=new(SelectedProfile,preferences.Mode,Slx.Client.Windows.ApplicationGroups.Expand(Applications.SelectMany(x=>x.ToRules())));
            await mutationGate.WaitAsync(current.Token);
            entered = true;
            if (action == "update-applications" && plan != null) RememberApplicationRevision(plan, routingOwner);
            var reply = await SendControlAsync(new(PipeContract.Version, action, Guid.NewGuid(), plan), current.Token);
            if (owner != generation || disposed) return;
            if (!reply.Success)
            {
                if (action == "update-applications" && RetainLiveRoutingFailure(reply)) return;
                MarkStatusUnknown(reply.Code); ShowFault(reply.Code); return;
            }
            status = reply.Status;
            statusFresh = true; lastServiceCode = null;
            if (status.Phase == TunnelPhase.Faulted && status.Code != null) ShowFault(status.Code);
            if (action != "disconnect" || status.Phase == TunnelPhase.Disconnected) ConfirmRoutingMutation(routingOwner);
        }
        catch (Exception error) when (error is ClientFault or OperationCanceledException or IOException)
        {
            if (owner != generation) return;
            var code = error is ClientFault fault ? fault.Code : error is OperationCanceledException ? "SERVICE_TIMEOUT" : "SERVICE_UNAVAILABLE";
            MarkStatusUnknown(code);
            if (action == "update-applications") Notice("RulesApplyUnconfirmed"); else ShowFault(code);
        }
        finally
        {
            if (entered) mutationGate.Release();
            if (ReferenceEquals(startupConnectionOperation, current)) startupConnectionOperation = null;
            if (owner == generation) { operation = null; IsBusy = false; }
        }
    }
    public async Task<bool> ExitAsync()
    {
        CancelStartupConnection();
        if (disposed) return true;
        timer.Stop();
        if (HasSubscription || IsTunnelActive || IsBusy)
        {
            await ChangeTunnelAsync("disconnect");
            if (!statusFresh || IsTunnelActive) { timer.Start(); return false; }
        }
        Dispose();
        Application.Current.Shutdown();
        return true;
    }
    private void MarkStatusUnknown(string code)
    {
        routingUnconfirmed |= RulesPending;
        statusFresh = false; lastServiceCode = ServiceStateCopy.SafeCode(code);
    }
    private void ShowFault(string code)
    {
        if (ServiceStateCopy.IsControlFault(code) || ServiceStateCopy.IsTrafficFault(code))
        {
            lastServiceCode = ServiceStateCopy.SafeCode(code);
            if (Page == "connection") ClearMessage(); else Notice(ServiceStateCopy.DetailKey(lastServiceCode));
            return;
        }
        Notice(code switch
    {
        "DEVICE_BOUND" => "FaultDeviceBound", "ACCESS_DENIED" or "EXPIRED" or "REVOKED" => "FaultAccess",
        "INVALID_URL" or "MISSING" => "FaultUrl", "NETWORK_UNAVAILABLE" or "SUBSCRIPTION_UNAVAILABLE" => "FaultNetwork",
        "INVALID_SUBSCRIPTION" => "FaultSubscription", "UNSUPPORTED_PROFILE" => "FaultProfile",
        "STARTUP_UNAVAILABLE" => "FaultStartup", "STORAGE_UNAVAILABLE" => "FaultStorage", "SERVICE_UNAVAILABLE" => "ServiceHelp",
        "OWNER_MISMATCH" or "OTHER_USER" or "CONTROLLER_OWNED" => "FaultOwner",
        "MULTI_SESSION_UNSUPPORTED" => "FaultSessions", "TUN_ADAPTER_CONFLICT" => "FaultOtherVpn", "TUN_CLEANUP_PENDING" => "FaultCleanup", "CORE_STOP_FAILED" => "FaultCore",
        "SERVICE_IDENTITY_FAILED" or "SERVICE_ACCESS_DENIED" => "FaultServiceSecurity", "CANCELLED" => "FaultCancelled",
        "TUN_ADDRESS_FAILED" or "ADAPTER_QUERY_FAILED" or "CORE_MISSING" or "CORE_FAILED" or "CORE_UNAVAILABLE" or "TUNNEL_NOT_READY" or "CORE_EXITED" or "CORE_START_FAILED" or "VPN_PROBE_FAILED" or "TUN_NOT_READY" => "FaultCore", _ => "FaultUnknown"
        });
    }
    public void ExitUiOnly() { Dispose(); Application.Current.Shutdown(); }
    private static void OpenLink(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException) { }
    }
    private void CloseOverlay()
    {
        if (IsBusy) return;
        ImportVisible = false; DiscoveryVisible = false; RemoveVisible = false; RemoveBlocked = false;
        OnPropertyChanged(nameof(IsOverlayVisible));
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; discoveryCancellation?.Cancel(); CancelRoutingApply(); CancelStartupRefresh(); timer.Stop(); timer.Tick -= PollTick; operation?.Cancel();
        libraryAddCancellation?.Cancel();foreach(var entry in libraryEntries.Values)entry.Dispose();
        Updates.Dispose();
        Text.Catalog.PropertyChanged -= LanguageChanged;
        GC.SuppressFinalize(this);
    }
}
