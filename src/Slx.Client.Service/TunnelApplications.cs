using System.Diagnostics;
using Slx.Client.Core;
using Slx.Client.Windows;
namespace Slx.Client.Service;
internal sealed partial class TunnelEngine
{
    private PendingApplicationUpdate? pendingApplicationUpdate;
    private long applicationReconcileAt;
    private sealed record PendingApplicationUpdate(ApplicationUpdateTransaction Transaction,
        ConnectPlan Before, ConnectPlan After, ConnectPlan PinnedBefore, ConnectPlan PinnedAfter);
    public async Task UpdateApplicationsAsync(ConnectPlan plan, string sid, CancellationToken token)
    {
        plan = ExactApplicationPlan.Prepare(plan, token);
        await lifecycle.WaitAsync(token);
        try
        {
            if (ownerSid != sid) throw new ClientFault("CONTROLLER_OWNED");
            if (pendingApplicationUpdate is not null)
            {
                await ReconcileApplicationsAsync();
                if (pendingApplicationUpdate is not null) throw new ClientFault("ROUTING_UPDATE_UNCONFIRMED");
            }
            var before = currentPlan ?? throw new ClientFault("ROUTING_NOT_READY");
            var pinnedBefore = pinnedPlan ?? throw new ClientFault("ROUTING_NOT_READY");
            if (!desiredConnected || state.Phase != TunnelPhase.Connected || !state.ProbePassed
                || child is null || child.HasExited || ownedAdapterId is null || !LocalProbe.TunnelReady())
                throw new ClientFault("ROUTING_NOT_READY");
            if (!ApplicationPlanChange.SameConnection(before, plan)) throw new ClientFault("ROUTING_CONNECTION_CHANGED");
            var pinnedAfter = pinnedBefore with { Applications = plan.Applications };
            if (ApplicationPlanChange.SameRoutes(before, plan))
            {
                // Metadata/disabled rows never touch WFP, DNS, the core or its existing streams.
                intents.Save(new(sid, pinnedAfter, plan.Profile.Host));
                currentPlan = plan; pinnedPlan = pinnedAfter;
                state = state with { ApplicationRevision = XrayRouteRevision.For(plan), Code = null };
                return;
            }
            var guard = trafficGuard ?? throw new ClientFault("TRAFFIC_PROTECTION_APPLICATION_BASELINE");
            var oldPolicy = guard.CurrentApplications;
            var newPolicy = guard.PrepareApplications(plan);
            var union = oldPolicy.Union(newPolicy);
            var control = new XrayRouteControl(probe.Port, probe.User, probe.Password);
            var transaction = new ApplicationUpdateTransaction(pinnedBefore, pinnedAfter,
                stage => guard.ApplyApplications(stage switch
                {
                    ApplicationProtectionStage.Before => oldPolicy,
                    ApplicationProtectionStage.After => newPolicy,
                    _ => union
                }),
                next => intents.Save(new(sid, next ? pinnedAfter : pinnedBefore,
                    next ? plan.Profile.Host : before.Profile.Host)), control.ReplaceAsync, control.ReadRevisionAsync);
            var pending = new PendingApplicationUpdate(transaction, before, plan, pinnedBefore, pinnedAfter);
            pendingApplicationUpdate = pending;
            try { await transaction.StartAsync(token); }
            catch
            {
                if (!RecoverApplicationProtection(transaction, before, plan, pinnedBefore, pinnedAfter)
                    && !transaction.Attempted) pendingApplicationUpdate = null;
                throw;
            }
            // The durable desired plan is adopted even if the command acknowledgement was lost.
            currentPlan = plan; pinnedPlan = pinnedAfter;
            PublishApplicationOutcome(pending);
            if (transaction.ProtectionRecoveryRequired) throw new ClientFault(transaction.FailureCode ?? "TRAFFIC_PROTECTION_FAILED");
            if (!transaction.Finished) throw new ClientFault("ROUTING_UPDATE_UNCONFIRMED");
            if (!transaction.Applied) throw new ClientFault("ROUTING_UPDATE_FAILED");
        }
        finally { lifecycle.Release(); }
    }
    private async Task ReconcileApplicationsAsync()
    {
        var pending = pendingApplicationUpdate;
        if (pending is null) return;
        applicationReconcileAt = Stopwatch.GetTimestamp() + 15 * Stopwatch.Frequency;
        await pending.Transaction.ReconcileAsync();
        PublishApplicationOutcome(pending);
    }
    private void PublishApplicationOutcome(PendingApplicationUpdate pending)
    {
        var transaction = pending.Transaction;
        if (RecoverApplicationProtection(transaction, pending.Before, pending.After, pending.PinnedBefore, pending.PinnedAfter)) return;
        if (!transaction.Finished)
        {
            state = state with { Code = "ROUTING_UPDATE_UNCONFIRMED", ApplicationRevision = null };
            applicationReconcileAt = Stopwatch.GetTimestamp() + 15 * Stopwatch.Frequency;
            return;
        }
        currentPlan = transaction.Applied ? pending.After : pending.Before;
        pinnedPlan = transaction.Applied ? pending.PinnedAfter : pending.PinnedBefore;
        state = state with { Code = transaction.Applied ? null : "ROUTING_UPDATE_FAILED",
            ApplicationRevision = XrayRouteRevision.For(currentPlan) };
        pendingApplicationUpdate = null;
    }
    internal bool RecoverApplicationProtection(ApplicationUpdateTransaction transaction,
        ConnectPlan before, ConnectPlan after, ConnectPlan pinnedBefore, ConnectPlan pinnedAfter)
    {
        if (!transaction.ProtectionRecoveryRequired) return false;
        // A failed native commit invalidates the delta baseline. Full protected recovery rebuilds it;
        // no AddRule replay and no claim that the old revision still has confirmed protection.
        currentPlan = transaction.RecoveryUsesAfter ? after : before;
        pinnedPlan = transaction.RecoveryUsesAfter ? pinnedAfter : pinnedBefore;
        pendingApplicationUpdate = null;
        ScheduleRecovery(transaction.FailureCode ?? "TRAFFIC_PROTECTION_FAILED");
        return true;
    }
}
