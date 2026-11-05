using Slx.Client.Core;

namespace Slx.Client.Service;

/// <summary>
/// The chosen server first, then its other transports. Two consecutive failed cycles on one transport move to
/// the next one, round-robin. Only transports of the user's chosen server/location are ever candidates.
/// </summary>
internal sealed class TransportRotation
{
    internal const int FailuresBeforeSwitch = 2;
    private IReadOnlyList<VpnProfile> candidates = [];
    private int failures;
    internal int Index { get; private set; }
    internal int Count => candidates.Count;
    /// <summary>Consecutive failed cycles since the last working connection.</summary>
    internal int Failures { get; private set; }
    internal string? Reason { get; private set; }
    internal VpnProfile Current => candidates.Count == 0 ? throw new ClientFault("NO_CONNECTION_PLAN") : candidates[Index];

    internal void Reset(ConnectPlan pinned)
    {
        ArgumentNullException.ThrowIfNull(pinned);
        candidates = Candidates(pinned);
        Index = 0; failures = 0; Failures = 0; Reason = null;
    }

    /// <summary>Keeps the current transport when the same server is re-pinned or its applications change.</summary>
    internal void Ensure(ConnectPlan pinned)
    {
        ArgumentNullException.ThrowIfNull(pinned);
        if (candidates.Count == 0 || candidates[0].Id != pinned.Profile.Id) { Reset(pinned); return; }
        var current = candidates[Index];
        var next = Candidates(pinned);
        int index = Array.FindIndex(next, profile => profile.Id == current.Id);
        candidates = next;
        Index = index < 0 ? 0 : index;
        if (index < 0) failures = 0;
    }

    internal void Clear() { candidates = []; Index = 0; failures = 0; Failures = 0; Reason = null; }

    /// <returns>True when this failure moved the rotation to another transport.</returns>
    internal bool Failed(string reason)
    {
        Reason = reason;
        Failures = Math.Min(Failures + 1, 9999);
        if (++failures < FailuresBeforeSwitch || candidates.Count < 2) return false;
        Index = (Index + 1) % candidates.Count;
        failures = 0;
        return true;
    }

    /// <summary>A whole readiness budget already failed while the user waits: try the next transport now.</summary>
    internal bool Advance(string reason)
    {
        Reason = reason;
        Failures = Math.Min(Failures + 1, 9999);
        failures = 0;
        if (candidates.Count < 2) return false;
        Index = (Index + 1) % candidates.Count;
        return true;
    }

    internal void Succeeded() { failures = 0; Failures = 0; }

    private static VpnProfile[] Candidates(ConnectPlan plan)
    {
        var list = new List<VpnProfile> { plan.Profile };
        if (plan.Alternates is not null) list.AddRange(plan.Alternates);
        return list.ToArray();
    }

    internal ConnectionAttemptObservation? Observe(DateTimeOffset at) => candidates.Count == 0 ? null
        : new(Current.Name, TransportAlternates.Label(Current), Index + 1, candidates.Count, Failures + 1, Reason, at);
}
