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
    private string? connectionFailureCode;
    private string? RetainedConnectionFailure => statusFresh
        && (status?.Phase is TunnelPhase.Disconnected or TunnelPhase.Faulted) ? connectionFailureCode : null;
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
    private Task ToggleAsync() => ShouldRequestDisconnect ? ChangeTunnelAsync("disconnect") : ChangeTunnelAsync("connect");
    private async Task ChangeTunnelAsync(string action)
    {
        if (action != "connect-if-idle") CancelStartupConnection();
        if (UpdateInProgress) return;
        var routingOwner = BeginRoutingMutation();
        if (action != "disconnect" && saved == null) { OpenImportCommand.Execute(null); return; }
        if (action != "disconnect" && IsSelectedMode && !HasSelectedApplications) { Notice("NoActiveApps"); Page = "applications"; return; }
        ConnectPlan? plan = null;
        var owner = ++generation;
        subscriptionOperation?.Cancel();
        subscriptionOperation = null;
        operation?.Cancel();
        using var current = new CancellationTokenSource(PipeContract.ClientMutationTimeout);
        operation = current;
        if (action == "connect-if-idle") startupConnectionOperation = current;
        connectionFailureCode = null; lastServiceCode = null;
        BeginControlObservation(action);
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
            var requestObservationRevision = observationRevision;
            var reply = await SendControlAsync(new(PipeContract.Version, action, Guid.NewGuid(), plan), current.Token);
            if (owner != generation || disposed) return;
            bool reconcile = requestObservationRevision != observationRevision;
            if (!reply.Success)
            {
                if (!(action == "update-applications" && RetainLiveRoutingFailure(reply, !reconcile)))
                {
                    RememberConnectionFailure(action, reply.Code);
                    if (!reconcile) MarkStatusUnknown(reply.Code);
                    ShowFault(reply.Code);
                }
            }
            else
            {
                if (!reconcile) AcceptStatus(reply.Status);
                lastServiceCode = null;
                if (!reconcile && status!.Phase == TunnelPhase.Faulted && status.Code != null) ShowFault(status.Code);
                if (action != "disconnect" || reply.Status.Phase == TunnelPhase.Disconnected) ConfirmRoutingMutation(routingOwner);
            }
            if (reconcile) await ReconcileControlObservationAsync(owner, current.Token);
        }
        catch (Exception error) when (error is ClientFault or OperationCanceledException or IOException)
        {
            if (owner != generation) return;
            var code = error is ClientFault fault ? fault.Code : error is OperationCanceledException ? "SERVICE_TIMEOUT" : "SERVICE_UNAVAILABLE";
            RememberConnectionFailure(action, code);
            MarkStatusUnknown(code);
            if (action == "update-applications") Notice("RulesApplyUnconfirmed"); else ShowFault(code);
        }
        finally
        {
            if (entered) mutationGate.Release();
            if (ReferenceEquals(startupConnectionOperation, current)) startupConnectionOperation = null;
            if (owner == generation) { operation = null; EndControlObservation(); IsBusy = false; }
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
        observationRevision++;
        routingUnconfirmed |= RulesPending;
        statusFresh = false; lastServiceCode = ServiceStateCopy.SafeCode(code);
    }
    private void ShowFault(string code)
    {
        if (ServiceStateCopy.IsDefinitiveConnectionFault(code)) { Notice(ServiceStateCopy.DetailKey(code)); return; }
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
        "OUTBOUND_INTERFACE_UNAVAILABLE" => "FaultOutboundInterface", "VPN_PROBE_FAILED" => "FaultVpnProbe",
        "VPN_TRAFFIC_ROUTE_FAILED" => "FaultVpnTrafficRoute",
        "SERVICE_IDENTITY_FAILED" or "SERVICE_ACCESS_DENIED" => "FaultServiceSecurity", "CANCELLED" => "FaultCancelled",
        "TUN_ADDRESS_FAILED" or "ADAPTER_QUERY_FAILED" or "CORE_MISSING" or "CORE_FAILED" or "CORE_UNAVAILABLE" or "TUNNEL_NOT_READY" or "CORE_EXITED" or "CORE_START_FAILED" or "TUN_NOT_READY" => "FaultCore", _ => "FaultUnknown"
        });
    }
    private void RememberConnectionFailure(string action, string code)
    {
        if ((action is "connect" or "connect-if-idle" or "reconnect") && ServiceStateCopy.IsDefinitiveConnectionFault(code))
            connectionFailureCode = ServiceStateCopy.SafeCode(code);
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
        disposed = true; discoveryCancellation?.Cancel(); CancelRoutingApply(); CancelStartupRefresh(); timer.Stop(); timer.Tick -= PollTick; operation?.Cancel(); subscriptionOperation?.Cancel();
        libraryAddCancellation?.Cancel();foreach(var entry in libraryEntries.Values)entry.Dispose();
        Updates.Dispose(); noticeTimer?.Stop();
        Text.Catalog.PropertyChanged -= LanguageChanged;
        GC.SuppressFinalize(this);
    }
}
