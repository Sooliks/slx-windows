using Slx.Client.Core;
using Slx.Client.Desktop.Localization;
using Slx.Client.Desktop.Presentation;
namespace Slx.Client.Desktop.ViewModels;
public sealed partial class MainViewModel
{
    private CancellationTokenSource? routingDelay;
    private bool connectionChangePending, routingUnconfirmed, routingDeferred;
    private long routingChangeVersion, pendingAppVersion;
    private string? pendingAppRevision;
    public string RoutingUpdateText => Text.Catalog[statusFresh && !routingUnconfirmed ? "RulesPending" : "RulesUnconfirmed"];
    internal Func<PipeRequest, CancellationToken, Task<PipeReply>>? SendOverride { get; set; }
    private Task<PipeReply> SendControlAsync(PipeRequest request, CancellationToken token) =>
        SendOverride?.Invoke(request, token) ?? service.SendAsync(request, token);
    private void CancelRoutingApply()
    {
        routingDelay?.Cancel();
        routingDelay = null;
    }
    private void MarkRulesChanged() => QueueRoutingApply(false);
    private void MarkConnectionChanged() => QueueRoutingApply(true);
    private void QueueRoutingApply(bool connectionChanged)
    {
        CancelRoutingApply();
        if (!IsTunnelActive) return;
        connectionChangePending |= connectionChanged;
        // Application-library edits cannot change whole-PC routes or erase an unconfirmed result.
        if (IsWholeMode && !connectionChangePending) return;
        routingChangeVersion++; pendingAppRevision = null;
        RulesPending = true;
        var delay = new CancellationTokenSource();
        routingDelay = delay;
        _ = ApplyRoutingAsync(delay);
    }
    private async Task ApplyRoutingAsync(CancellationTokenSource delay)
    {
        try
        {
            // Coalesce rapid toggles before starting one mutation. Never retry an unknown result.
            await Task.Delay(400, delay.Token);
            if (disposed || !IsTunnelActive) return;
            // Nothing was sent yet: keep the edit and submit it after the next authoritative status, not never.
            if (!statusFresh || IsBusy) { routingDeferred = true; return; }
            await ChangeTunnelAsync(IsSelectedMode && !HasSelectedApplications ? "disconnect"
                : connectionChangePending ? "reconnect" : "update-applications");
        }
        catch (OperationCanceledException) when (delay.IsCancellationRequested) { }
        finally
        {
            if (ReferenceEquals(routingDelay, delay)) routingDelay = null;
            delay.Dispose();
        }
    }
    internal void ResumeDeferredRouting()
    {
        if (!routingDeferred || routingDelay != null || disposed || IsBusy || !statusFresh) return;
        routingDeferred = false;
        if (RulesPending && IsTunnelActive) QueueRoutingApply(false);
    }
    private long BeginRoutingMutation()
    {
        CancelRoutingApply(); connectionChangePending = false; pendingAppRevision = null; routingDeferred = false;
        return routingChangeVersion;
    }
    private void RememberApplicationRevision(ConnectPlan plan, long version)
    {
        if (version != routingChangeVersion) return;
        pendingAppRevision = XrayRouteRevision.For(plan); pendingAppVersion = version;
    }
    private void ConfirmRoutingMutation(long version)
    {
        if (version != routingChangeVersion) return;
        RulesPending = false; routingUnconfirmed = false; pendingAppRevision = null;
    }
    private void ReconcileApplicationRevision()
    {
        if (!RulesPending || !IsConnected || status?.ProtectionActive != true || connectionChangePending || pendingAppRevision is null
            || pendingAppVersion != routingChangeVersion || status?.ApplicationRevision != pendingAppRevision) return;
        ConfirmRoutingMutation(pendingAppVersion);
        if (messageKey is "RulesApplyFailed" or "RulesApplyUnconfirmed" or "RulesConnectionChanged") ClearMessage();
    }
    private bool RetainLiveRoutingFailure(PipeReply reply)
    {
        if (reply.Status is not { Phase: TunnelPhase.Connected, ProbePassed: true, ProtectionActive: true }) return false;
        status = reply.Status; statusFresh = true; lastServiceCode = ServiceStateCopy.SafeCode(reply.Code);
        RulesPending = true; routingUnconfirmed = true;
        Notice(reply.Code switch
        {
            "ROUTING_UPDATE_UNCONFIRMED" => "RulesApplyUnconfirmed",
            "ROUTING_CONNECTION_CHANGED" => "RulesConnectionChanged", _ => "RulesApplyFailed"
        });
        return true;
    }
}
