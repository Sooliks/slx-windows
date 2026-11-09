using System.Text.Json.Serialization;
namespace Slx.Client.Core;

public sealed record TransportAttemptSnapshot(string Transport, int Index, int Count, int Attempt, DateTimeOffset At);
public sealed record TransportExecutionObservation(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] TransportAttemptSnapshot? Current,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] TransportAttemptSnapshot? LastFailed,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] TransportAttemptSnapshot? Next);
