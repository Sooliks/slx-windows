namespace Slx.Client.Core;

/// <summary>Which transport of the chosen server the core uses now, and why the last recovery happened.</summary>
public sealed record ConnectionAttemptObservation(string ProfileName, string Transport, int TransportIndex,
    int TransportCount, int Attempt, string? Reason, DateTimeOffset At);
