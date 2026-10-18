using System.IO;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using Slx.Client.Core;
using Slx.Client.Desktop.Localization;
namespace Slx.Client.Desktop.ViewModels;
public sealed partial class MainViewModel
{
    private readonly SemaphoreSlim mutationGate = new(1, 1);
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(3) };
    private CancellationTokenSource? operation;
    private long generation;
    private bool polling, disposed;
    public void Initialize()
    {
        timer.Tick += PollTick;
        timer.Start();
        _ = PollAsync();
    }
    private async void PollTick(object? sender, EventArgs e) => await PollAsync();
    private async Task PollAsync()
    {
        if (polling || IsBusy || disposed) return;
        polling = true;
        var owner = generation;
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var reply = await service.SendAsync(new(PipeContract.Version, "status", Guid.NewGuid()), timeout.Token);
            if (owner != generation || disposed) return;
            if (!reply.Success) { statusFresh = false; ShowFault(reply.Code); return; }
            status = reply.Status;
            statusFresh = true;
            if (status.Phase == TunnelPhase.Faulted && status.Code != null) ShowFault(status.Code);
        }
        catch (Exception error) when (error is ClientFault or OperationCanceledException or IOException)
        { if (owner == generation) statusFresh = false; }
        finally { polling = false; NotifyState(); }
    }
    private Task ToggleAsync() => IsTunnelActive || IsBusy ? ChangeTunnelAsync("disconnect") : ChangeTunnelAsync("connect");
    private async Task ChangeTunnelAsync(string action)
    {
        if (action != "disconnect" && saved == null) { OpenImportCommand.Execute(null); return; }
        if (action != "disconnect" && IsSelectedMode && !HasSelectedApplications) { Notice("NoActiveApps"); Page = "applications"; return; }
        ConnectPlan? plan = action == "disconnect" || SelectedProfile == null ? null : new(SelectedProfile, preferences.Mode, Applications.Select(x => x.ToRule()).ToList());
        var owner = ++generation;
        operation?.Cancel();
        using var current = new CancellationTokenSource(TimeSpan.FromSeconds(55));
        operation = current;
        IsBusy = true;
        ClearMessage();
        var entered = false;
        try
        {
            await mutationGate.WaitAsync(current.Token);
            entered = true;
            var reply = await service.SendAsync(new(PipeContract.Version, action, Guid.NewGuid(), plan), current.Token);
            if (owner != generation || disposed) return;
            if (!reply.Success) { statusFresh = false; ShowFault(reply.Code); return; }
            status = reply.Status;
            statusFresh = true;
            if (status.Phase == TunnelPhase.Faulted && status.Code != null) ShowFault(status.Code);
            if (action != "disconnect") RulesPending = false;
        }
        catch (Exception error) when (error is ClientFault or OperationCanceledException or IOException)
        {
            if (owner != generation) return;
            statusFresh = false;
            ShowFault(error is ClientFault fault ? fault.Code : error is OperationCanceledException ? "CANCELLED" : "SERVICE_UNAVAILABLE");
        }
        finally
        {
            if (entered) mutationGate.Release();
            if (owner == generation) { operation = null; IsBusy = false; }
        }
    }
    public async Task<bool> ExitAsync()
    {
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
    private void ShowFault(string code) => Notice(code switch
    {
        "DEVICE_BOUND" => "FaultDeviceBound", "ACCESS_DENIED" or "EXPIRED" or "REVOKED" => "FaultAccess",
        "INVALID_URL" or "MISSING" => "FaultUrl", "NETWORK_UNAVAILABLE" or "SUBSCRIPTION_UNAVAILABLE" => "FaultNetwork",
        "INVALID_SUBSCRIPTION" => "FaultSubscription", "UNSUPPORTED_PROFILE" => "FaultProfile",
        "STORAGE_UNAVAILABLE" => "FaultStorage", "SERVICE_UNAVAILABLE" => "ServiceHelp",
        "OWNER_MISMATCH" or "OTHER_USER" or "CONTROLLER_OWNED" => "FaultOwner",
        "MULTI_SESSION_UNSUPPORTED" => "FaultSessions", "TUN_ADAPTER_CONFLICT" => "FaultOtherVpn",
        "SERVICE_IDENTITY_FAILED" or "SERVICE_ACCESS_DENIED" => "FaultServiceSecurity", "CANCELLED" => "FaultCancelled",
        "CORE_MISSING" or "CORE_FAILED" or "CORE_UNAVAILABLE" or "TUNNEL_NOT_READY" or "CORE_EXITED" or "CORE_START_FAILED" or "VPN_PROBE_FAILED" or "TUN_NOT_READY" => "FaultCore", _ => "FaultUnknown"
    });
    private static void OpenLink(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException) { }
    }
    private void CloseOverlay()
    {
        if (IsBusy) return;
        ImportVisible = false; DiscoveryVisible = false; RemoveVisible = false;
        OnPropertyChanged(nameof(IsOverlayVisible));
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; timer.Stop(); timer.Tick -= PollTick; operation?.Cancel();
        Text.Catalog.PropertyChanged -= LanguageChanged;
        GC.SuppressFinalize(this);
    }
}
