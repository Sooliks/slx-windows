using Slx.Client.Core;

namespace Slx.Client.Service;

internal sealed partial class ControlDispatcher
{
    private readonly object observation = new();
    private ActiveOperation? active;
    private int pendingStops;
    private sealed record ActiveOperation(Guid Id, string Fingerprint, string Action, CancellationTokenSource Cancellation);

    private PipeReply Observe(string sid, PipeRequest request)
    {
        if (request.Plan is not null) return Rejected("IPC_INVALID");
        // Ownership changes share this short lock; a network wait never owns it. Read the immutable
        // observation only after authorization so a different Windows user cannot inspect the plan.
        lock (observation)
        {
            RefreshOwner();
            return owner is not null && owner != sid || !CanReadMaintenance(sid)
                ? Rejected("CONTROLLER_OWNED") : new(true, "OK", ObservedStatus);
        }
    }

    private (PipeReply? Reply, IDisposable? Admission) PrepareDisconnect(string sid, PipeRequest request)
    {
        if (request.Plan is not null) return (Rejected("IPC_INVALID"), null);
        string fingerprint = Fingerprint(sid, request);
        ActiveOperation? interrupted;
        lock (observation)
        {
            RefreshOwner();
            if (owner is not null && owner != sid) return (Rejected("CONTROLLER_OWNED"), null);
            PruneReceipts();
            if (receipts.TryGetValue(request.RequestId, out var prior))
                return (prior.Fingerprint == fingerprint ? prior.Reply with { Status = ObservedStatus }
                    : Rejected("IPC_REPLAY_MISMATCH"), null);
            if (active is { } running && running.Id == request.RequestId && running.Fingerprint != fingerprint)
                return (Rejected("IPC_REPLAY_MISMATCH"), null);
            pendingStops++;
            interrupted = active is { Action: not "disconnect" } ? active : null;
        }
        // Explicit off can cancel a long connect before waiting for the mutation gate. It never
        // cancels another Stop, executes concurrently with a mutation, or replays an unknown start.
        try { interrupted?.Cancellation.Cancel(); }
        catch (ObjectDisposedException) { } // The operation finished between the snapshot and cancellation.
        return (null, new StopAdmission(this));
    }

    private void RefreshOwner()
    {
        // A failed explicit off retains its controller until the engine confirms that the full
        // asynchronous off-cleanup completed, not merely a missing SID or a placeholder status.
        if (active is null && engine.IsConfirmedOff) owner = null;
        owner ??= engine.OwnerSid;
    }

    private sealed class StopAdmission(ControlDispatcher dispatcher) : IDisposable
    {
        private int disposed;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0) return;
            lock (dispatcher.observation) dispatcher.pendingStops--;
        }
    }
}
