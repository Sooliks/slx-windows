using Slx.Client.Core;

namespace Slx.Client.Service;

internal sealed partial class ControlDispatcher
{
    private readonly Dictionary<Guid, PendingIdentity> admitted = new();
    private sealed class PendingIdentity(string fingerprint)
    {
        public string Fingerprint { get; } = fingerprint;
        public int Users { get; set; }
    }

    private (PipeReply? Reply, IDisposable? Admission) AdmitMutation(string sid, PipeRequest request)
    {
        string fingerprint = Fingerprint(sid, request);
        lock (observation)
        {
            RefreshOwner();
            if (owner is not null && owner != sid) return (Rejected("CONTROLLER_OWNED"), null);
            PruneReceipts();
            if (receipts.TryGetValue(request.RequestId, out var prior))
                return (prior.Fingerprint == fingerprint ? prior.Reply with { Status = engine.Status }
                    : Rejected("IPC_REPLAY_MISMATCH"), null);
            // Reserve identity before waiting for the mutation gate or performing validation.
            // A changed disconnect with this ID must not cancel an admitted connect while no
            // active cancellation source or completed receipt exists yet.
            if (!admitted.TryGetValue(request.RequestId, out var pending))
                admitted.Add(request.RequestId, pending = new(fingerprint));
            else if (pending.Fingerprint != fingerprint) return (Rejected("IPC_REPLAY_MISMATCH"), null);
            pending.Users++;
            return (null, new MutationAdmission(this, request.RequestId, pending));
        }
    }

    private sealed class MutationAdmission(ControlDispatcher dispatcher, Guid id, PendingIdentity pending) : IDisposable
    {
        private int disposed;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0) return;
            lock (dispatcher.observation)
            {
                if (--pending.Users == 0) dispatcher.admitted.Remove(id);
            }
        }
    }
}
