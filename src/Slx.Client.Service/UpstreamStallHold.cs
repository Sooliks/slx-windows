using System.Diagnostics;

namespace Slx.Client.Service;

/// <summary>
/// Bounded wait while fresh probes fail but an existing stream through the same core still answers.
/// </summary>
internal sealed class UpstreamStallHold
{
    private const int WindowSeconds = 45;
    internal static readonly TimeSpan RecheckInterval = TimeSpan.FromSeconds(5);
    private readonly Func<long> timestamp;
    private readonly long frequency;
    private long? started;

    internal UpstreamStallHold(Func<long>? clock = null, long? ticksPerSecond = null)
    {
        timestamp = clock ?? Stopwatch.GetTimestamp;
        frequency = ticksPerSecond ?? Stopwatch.Frequency;
        if (frequency <= 0 || frequency > long.MaxValue / 3600) throw new ArgumentOutOfRangeException(nameof(ticksPerSecond));
    }

    internal bool Pending => started is not null;
    internal int Holds { get; private set; }
    internal TimeSpan Remaining
    {
        get
        {
            if (started is not { } first) return TimeSpan.Zero;
            long now = timestamp();
            if (now < first) return TimeSpan.Zero;
            long remaining = Math.Max(0, WindowSeconds * frequency - (now - first));
            return TimeSpan.FromSeconds(remaining / (double)frequency);
        }
    }

    /// <returns>True when the current failure may keep the core; false once the window is spent.</returns>
    internal bool Hold()
    {
        started ??= timestamp();
        if (Remaining == TimeSpan.Zero) return false;
        Holds = Math.Min(Holds + 1, int.MaxValue - 1);
        return true;
    }

    /// <summary>A passed check, explicit off, replacement or an actual recovery ends the current window.</summary>
    internal void Reset() => started = null;
}
