using System.Diagnostics;
using Slx.Client.Core;

namespace Slx.Client.Service;

/// <summary>A bounded remote-only outage does not immediately destroy unselected direct streams in the shared TUN.</summary>
internal sealed class RemoteHealthGrace
{
    private readonly Func<long> timestamp;
    private readonly long frequency;
    private long? started;
    internal RemoteHealthGrace(Func<long>? clock = null, long? ticksPerSecond = null)
    {
        timestamp = clock ?? Stopwatch.GetTimestamp; frequency = ticksPerSecond ?? Stopwatch.Frequency;
        if (frequency <= 0 || frequency > long.MaxValue / 90) throw new ArgumentOutOfRangeException(nameof(ticksPerSecond));
    }
    internal bool Pending => started is not null;
    internal TimeSpan Remaining
    {
        get
        {
            if (started is not { } first) return TimeSpan.Zero;
            long now = timestamp();
            if (now < first) return TimeSpan.Zero;
            long remaining = Math.Max(0, 90 * frequency - (now - first));
            return TimeSpan.FromSeconds(remaining / (double)frequency);
        }
    }
    internal bool Defer(RoutingMode mode)
    {
        if (mode != RoutingMode.SelectedApplications) { Reset(); return false; }
        started ??= timestamp();
        return Remaining > TimeSpan.Zero;
    }
    internal void Reset() => started = null;
}
