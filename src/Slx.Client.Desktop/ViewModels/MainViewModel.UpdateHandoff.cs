using System.IO;
using Slx.Client.Core;
using Slx.Client.Windows;
namespace Slx.Client.Desktop.ViewModels;
public sealed partial class MainViewModel
{
    internal Func<PreparedUpdate, Guid, bool, string?, CancellationToken, Task>? UpdateHandoffOverride { get; set; }
    internal async Task<UpdateApplyResult> ApplyIntegratedUpdateAsync(PreparedUpdate prepared, bool resume,
        IProgress<UpdateApplyProgress> progress, CancellationToken cancellationToken)
    {
        if (IsBusy || UpdateInProgress || HasUpdateMaintenance) throw new ClientFault("UPDATE_CONNECTION_ACTIVE");
        CancelStartupRefresh(); CancelRoutingApply();
        await mutationGate.WaitAsync(cancellationToken);
        updateInstalling = true; NotifyState();
        var session = Guid.NewGuid(); bool preparedService = false, handedOff = false;
        string? fingerprint = resume ? UpdateFingerprint() : null;
        var owner = ++generation;
        try
        {
            progress.Report(new("preparing"));
            using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            budget.CancelAfter(PipeContract.ClientMutationTimeout);
            var read = await SendControlAsync(new(PipeContract.Version, "status", Guid.NewGuid()), budget.Token);
            RequireUpdateReply(read, owner);
            if (read.Status.ServiceVersion != VersionLabel["SLX VPN · ".Length..]) throw new ClientFault("UPDATE_SERVICE_VERSION");
            if (read.Status.UpdateMaintenance is not null || read.Status.Code == "UPDATE_MAINTENANCE_UNCONFIRMED")
                throw new ClientFault("UPDATE_MAINTENANCE_ACTIVE");
            if (read.Status.Phase != TunnelPhase.Disconnected || read.Status.ProtectionActive)
            {
                if (!resume || fingerprint is null) throw new ClientFault("UPDATE_CONNECTION_ACTIVE");
                BeginControlObservation("disconnect");
                var off = await SendControlAsync(new(PipeContract.Version, "disconnect", Guid.NewGuid()), budget.Token);
                RequireUpdateReply(off, owner);
                if (off.Status.Phase != TunnelPhase.Disconnected || off.Status.ProtectionActive)
                    throw new ClientFault("UPDATE_CONNECTION_ACTIVE");
            }
            // The service repeats its off check under the mutation gate and persists this exclusion.
            var preparedReply = await SendControlAsync(new(PipeContract.Version, "prepare-update", Guid.NewGuid(), UpdateSessionId: session), budget.Token);
            RequireUpdateReply(preparedReply, owner);
            if (preparedReply.Status.UpdateMaintenance?.SessionId != session) throw new ClientFault("UPDATE_HANDOFF_FAILED");
            preparedService = true;
            if (UpdateHandoffOverride is { } handoff) await handoff(prepared, session, resume, fingerprint, cancellationToken);
            else await new IntegratedUpdateClient().StageAndLaunchAsync(prepared, session, resume, Language, cancellationToken, fingerprint);
            handedOff = true;
            return UpdateApplyResult.Restarting;
        }
        catch (Exception error) when (error is ClientFault or IOException or OperationCanceledException)
        {
            if (!preparedService && error is (IOException or OperationCanceledException)) MarkStatusUnknown("SERVICE_TIMEOUT");
            if (cancellationToken.IsCancellationRequested) return UpdateApplyResult.Cancelled;
            throw new ClientFault(error is ClientFault fault ? fault.Code : "UPDATE_HANDOFF_FAILED");
        }
        finally
        {
            if (preparedService && !handedOff) await ReleaseUpdateMaintenanceAsync(session);
            EndControlObservation(); updateInstalling = false; mutationGate.Release(); NotifyState();
        }
    }
    private void RequireUpdateReply(PipeReply reply, long owner)
    {
        if (disposed || generation != owner) throw new ClientFault("UPDATE_HANDOFF_FAILED");
        if (!reply.Success) throw new ClientFault(reply.Code);
        AcceptStatus(reply.Status);
    }
    private string? UpdateFingerprint()
    {
        if (SelectedProfile is null || saved is null) return null;
        try { return XrayRouteRevision.For(new(SelectedProfile, preferences.Mode, ApplicationGroups.Expand(Applications.SelectMany(x => x.ToRules())))); }
        catch (ClientFault) { return null; }
    }
    private async Task<bool> ReleaseUpdateMaintenanceAsync(Guid session)
    {
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            var reply = await SendControlAsync(new(PipeContract.Version, "release-update", Guid.NewGuid(), UpdateSessionId: session), budget.Token);
            if (!disposed && reply.Success) { AcceptStatus(reply.Status); return true; }
        }
        catch (Exception error) when (error is ClientFault or IOException or OperationCanceledException) { }
        return false;
    }
}
