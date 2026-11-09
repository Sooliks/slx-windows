namespace Slx.Client.Core;

public enum PhysicalPathState { Ready, Unavailable, Changed }
public sealed record NetworkPathObservation(DateTimeOffset At, PhysicalPathState Physical,
    DateTimeOffset? LocalControlAt = null, bool? LocalControlReady = null);
