using System.Text;
using Slx.Client.Core;

namespace Slx.Client.Desktop.Presentation;

internal static class LifecycleDiagnostics
{
    public static void Append(StringBuilder text, TunnelLifecycleObservation? lifecycle)
    {
        if (lifecycle is null) return;
        var events = (lifecycle.Events ?? []).OfType<TunnelLifecycleEvent>()
            .TakeLast(TunnelLifecycleObservation.MaximumEvents).ToArray();
        text.AppendLine($"Lifecycle since UTC: {lifecycle.Since:O}");
        text.AppendLine($"Lifecycle generations: tun={lifecycle.TunGeneration}, core={lifecycle.CoreGeneration}");
        text.AppendLine($"Lifecycle retained events: {events.Length}, limit={TunnelLifecycleObservation.MaximumEvents}");
        foreach (var entry in events)
        {
            text.Append($"Lifecycle event {entry.Sequence}: {entry.At:O}, origin={Label(entry.Origin)}, "
                + $"scope={Label(entry.Scope)}, reason={Label(entry.Reason)}, outcome={Label(entry.Outcome)}, "
                + $"tun_generation={entry.TunGeneration}, core_generation={entry.CoreGeneration}");
            if (TunnelLifecycleObservation.SafeCode(entry.Code) is { } code) text.Append($", code={code}");
            text.AppendLine();
        }
        text.AppendLine("Lifecycle meaning: bounded service history; request origin does not prove a manual click; "
            + "generations count started processes, not uninterrupted traffic");
    }

    private static string Label<T>(T value) where T : struct, Enum => Enum.IsDefined(value) ? value.ToString() : "Unknown";
}
