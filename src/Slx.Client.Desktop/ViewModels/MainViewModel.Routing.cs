using Slx.Client.Core;
using Slx.Client.Desktop.Localization;
namespace Slx.Client.Desktop.ViewModels;
public sealed partial class MainViewModel
{
    private CancellationTokenSource? routingDelay;
    public string RoutingUpdateText => Text.Catalog[statusFresh ? "RulesPending" : "RulesUnconfirmed"];
    internal Func<PipeRequest, CancellationToken, Task<PipeReply>>? SendOverride { get; set; }
    private Task<PipeReply> SendControlAsync(PipeRequest request, CancellationToken token) =>
        SendOverride?.Invoke(request, token) ?? service.SendAsync(request, token);
    private void CancelRoutingApply()
    {
        routingDelay?.Cancel();
        routingDelay = null;
    }
    private void MarkRulesChanged()
    {
        CancelRoutingApply();
        if (!IsTunnelActive) return;
        if (IsWholeMode && status?.Mode == RoutingMode.WholeComputer && status.ProfileName == SelectedProfile?.Name) { RulesPending = false; return; }
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
            if (disposed || !statusFresh || IsBusy || !IsTunnelActive) return;
            await ChangeTunnelAsync(IsSelectedMode && !HasSelectedApplications ? "disconnect" : "reconnect");
            if (statusFresh && status?.Phase == TunnelPhase.Disconnected) RulesPending = false;
        }
        catch (OperationCanceledException) when (delay.IsCancellationRequested) { }
        finally
        {
            if (ReferenceEquals(routingDelay, delay)) routingDelay = null;
            delay.Dispose();
        }
    }
}
