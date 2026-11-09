using System.Text;
using Slx.Client.Core;
namespace Slx.Client.Desktop.Presentation;

internal static class TransportDiagnostics
{
    internal static string SafeTransport(string? value) => value is "hysteria2" or "vless-xhttp" or "vless-reality"
        ? value : "unknown";
    internal static void Append(StringBuilder text, TransportExecutionObservation? execution)
    {
        if (execution is null) return;
        AppendAttempt(text, "current attempt", execution.Current);
        AppendAttempt(text, "last failed attempt", execution.LastFailed);
        AppendAttempt(text, "next candidate", execution.Next);
        text.AppendLine("Transport meaning: current is a started attempt, not proof of traffic; next has not started");
    }
    private static void AppendAttempt(StringBuilder text, string label, TransportAttemptSnapshot? attempt)
        => text.AppendLine(attempt is null ? $"Transport {label}: none" : $"Transport {label}: {SafeTransport(attempt.Transport)}, "
            + $"index={attempt.Index}/{attempt.Count}, attempt={attempt.Attempt}, at={attempt.At:O}");
}
