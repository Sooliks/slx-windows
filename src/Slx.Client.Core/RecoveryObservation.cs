namespace Slx.Client.Core;
public enum RecoveryCause { ProbeFailed, CoreExited, AdapterUnavailable }
public sealed record RecoveryObservation(int Count, RecoveryCause Cause, DateTimeOffset At);
