using Slx.Client.Core;
namespace Slx.Client.Service;

/// <summary>Snapshots belong to an executed attempt; rotation cannot rewrite their transport or time.</summary>
internal sealed class TransportExecution
{
    internal TransportAttemptSnapshot? Current { get; private set; }
    internal TransportAttemptSnapshot? LastFailed { get; private set; }
    internal void Starting(TransportRotation rotation, DateTimeOffset at) => Current = Candidate(rotation, at);
    internal void Failed() { if (Current is not null) LastFailed = Current; Current = null; }
    internal void Stopped() => Current = null;
    internal TransportExecutionObservation Observe(TransportRotation rotation, DateTimeOffset at)
        => new(Current, LastFailed, Current is null && rotation.Count > 0 ? Candidate(rotation, at) : null);
    internal static TransportAttemptSnapshot Candidate(TransportRotation rotation, DateTimeOffset at)
        => new(TransportAlternates.Label(rotation.Current), rotation.Index + 1, rotation.Count, rotation.Failures + 1, at);
}
