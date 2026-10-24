using System.Diagnostics;
using Slx.Client.Core;

namespace Slx.Client.Service;

/// <summary>Retains an explicit off/initial rollback until its complete owned cleanup succeeds.</summary>
internal sealed class PendingOffCleanup(Func<long>? timestamp = null, long? frequency = null)
{
    private readonly Func<long> clock = timestamp ?? Stopwatch.GetTimestamp;
    private readonly long ticksPerSecond = frequency ?? Stopwatch.Frequency;
    private long retryAt;
    private int failures;
    public bool Pending { get; private set; }
    public string? CompletionCode { get; private set; }

    public void Begin(string? completionCode = null)
    {
        Pending = true;
        CompletionCode = completionCode;
        failures = 0;
        retryAt = 0;
    }

    public async Task<bool> TryAsync(Func<Task> cleanup, bool force = false)
    {
        if (!Pending || !force && clock() < retryAt) return false;
        try
        {
            await cleanup();
            Pending = false;
            return true;
        }
        catch
        {
            failures = Math.Min(failures + 1, 30);
            retryAt = clock() + (long)(RecoveryBackoff.Delay(failures).TotalSeconds * ticksPerSecond);
            throw;
        }
    }
}
