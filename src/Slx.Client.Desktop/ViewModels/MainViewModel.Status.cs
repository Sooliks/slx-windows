using System.IO;
using System.Diagnostics.CodeAnalysis;
using Slx.Client.Core;
using Slx.Client.Desktop.Presentation;
namespace Slx.Client.Desktop.ViewModels;
public sealed partial class MainViewModel
{
    // Local revisions order asynchronous replies. Service timestamps describe state transitions,
    // so neither wall-clock order nor ObservedAt can identify an obsolete pending read.
    private long observationRevision;
    private DateTimeOffset? serviceReadAt, controlOperationStartedAt;
    private string? pendingControlAction;
    private long? controlReconcileOwner;
    private async void PollTick(object? sender, EventArgs e) => await PollAsync();
    internal async Task PollAsync()
    {
        // The service has independent pipe slots for observations during long mutations.
        // Keep one bounded read in flight; a busy connect must not freeze its previous snapshot.
        if (polling || disposed || controlReconcileOwner is not null) return;
        polling = true;
        var owner = generation;
        var revision = observationRevision;
        bool CanAccept() => owner == generation && revision == observationRevision && !disposed;
        try
        {
            using var timeout = new CancellationTokenSource(PipeContract.ClientStatusTimeout);
            var reply = await SendControlAsync(new(PipeContract.Version, "status", Guid.NewGuid()), timeout.Token);
            if (!CanAccept()) return;
            if (!reply.Success) { MarkStatusUnknown(reply.Code); ShowFault(reply.Code); return; }
            AcceptFreshServiceStatus(reply.Status);
        }
        catch (Exception error) when (error is ClientFault or OperationCanceledException or IOException)
        { if (CanAccept()) MarkStatusUnknown(error is ClientFault fault ? fault.Code : "SERVICE_TIMEOUT"); }
        finally { polling = false; NotifyState(); }
        ResumeDeferredRouting();
        await TryStartupConnectionAsync();
    }
    [MemberNotNull(nameof(status))]
    private void AcceptStatus(TunnelStatus observed)
    {
        observationRevision++;
        status = observed;
        statusFresh = true;
        serviceReadAt = DateTimeOffset.UtcNow;
    }
    private void AcceptFreshServiceStatus(TunnelStatus observed)
    {
        AcceptStatus(observed);
        ReconcileApplicationRevision();
        if (status!.Phase == TunnelPhase.Connected && status.ProbePassed) connectionFailureCode = null;
        if (!ServiceStateCopy.IsTrafficFault(lastServiceCode) || status.Phase == TunnelPhase.Connected && status.ProbePassed) lastServiceCode = null;
        if (status.Phase == TunnelPhase.Faulted && status.Code != null) ShowFault(status.Code);
    }
    private async Task ReconcileControlObservationAsync(long owner, CancellationToken cancellationToken)
    {
        // A known mutation reply and a concurrently accepted read cannot order each other's snapshots.
        // Fence existing polls and read once after the reply; never replay the known mutation.
        var revision = ++observationRevision;
        controlReconcileOwner = owner;
        statusFresh = false;
        EndControlObservation();
        NotifyState();
        bool CanAccept() => owner == generation && revision == observationRevision && !disposed;
        try
        {
            using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            budget.CancelAfter(PipeContract.ClientStatusTimeout);
            var reply = await SendControlAsync(new(PipeContract.Version, "status", Guid.NewGuid()), budget.Token).WaitAsync(budget.Token);
            if (!CanAccept()) return;
            if (!reply.Success) { MarkStatusUnknown(reply.Code); ShowFault(reply.Code); return; }
            AcceptFreshServiceStatus(reply.Status);
        }
        catch (Exception error) when (error is ClientFault or OperationCanceledException or IOException)
        { if (CanAccept()) MarkStatusUnknown(error is ClientFault fault ? fault.Code : "SERVICE_TIMEOUT"); }
        finally
        {
            if (controlReconcileOwner == owner) controlReconcileOwner = null;
            NotifyState();
        }
    }
    private void BeginControlObservation(string action)
    {
        observationRevision++;
        controlReconcileOwner = null;
        statusFresh = false;
        pendingControlAction = action;
        controlOperationStartedAt = DateTimeOffset.UtcNow;
    }
    private void EndControlObservation()
    {
        pendingControlAction = null;
        controlOperationStartedAt = null;
    }
}
