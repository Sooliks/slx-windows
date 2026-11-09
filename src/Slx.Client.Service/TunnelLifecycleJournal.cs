using Slx.Client.Core;

namespace Slx.Client.Service;

/// <summary>Bounded, secret-free service history; status reads cannot observe a partially updated event.</summary>
internal sealed class TunnelLifecycleJournal
{
    private readonly object sync = new();
    private readonly DateTimeOffset since = DateTimeOffset.UtcNow;
    private readonly Queue<TunnelLifecycleEvent> events = new();
    private int tunGeneration, coreGeneration;
    private long sequence;

    internal void TunStarted() { lock (sync) tunGeneration = Math.Min(tunGeneration, int.MaxValue - 1) + 1; }
    internal void CoreStarted() { lock (sync) coreGeneration = Math.Min(coreGeneration, int.MaxValue - 1) + 1; }

    internal void Record(TunnelLifecycleOrigin origin, CoreRecoveryDecision decision,
        TunnelLifecycleOutcome outcome, string? code = null, TransportAttemptSnapshot? transport = null)
    {
        lock (sync)
        {
            sequence = Math.Min(sequence, long.MaxValue - 1) + 1;
            events.Enqueue(new(sequence, DateTimeOffset.UtcNow, origin, decision.Scope, decision.Reason, outcome,
                tunGeneration, coreGeneration, TunnelLifecycleObservation.SafeCode(code), transport));
            while (events.Count > TunnelLifecycleObservation.MaximumEvents) events.Dequeue();
        }
    }

    internal TunnelLifecycleObservation Snapshot()
    {
        lock (sync) return new(since, tunGeneration, coreGeneration, events.ToArray());
    }
}
