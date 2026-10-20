using System.Diagnostics;
using Slx.Client.Core;
namespace Slx.Client.Service;
/// <summary>Retry ownership for cleanup after a successfully read, absent on-intent only.</summary>
internal sealed class StartupOffCleanup
{
    public const string PendingCode="SERVICE_STARTUP_CLEANUP_PENDING";
    private readonly Func<long> timestamp;
    private readonly long frequency;
    private long retryAt;
    private int failures;
    public bool Pending{get;private set;}
    public StartupOffCleanup(Func<long>? timestamp=null,long? frequency=null)
    {
        this.timestamp=timestamp??Stopwatch.GetTimestamp;this.frequency=frequency??Stopwatch.Frequency;
    }
    public void Begin(){Pending=true;failures=0;retryAt=0;}
    public void Cancel(){Pending=false;failures=0;retryAt=0;}
    public bool TryRelease(Action release)
    {
        if(!Pending||timestamp()<retryAt)return false;
        try{release();Cancel();return true;}
        catch
        {
            failures=Math.Min(failures+1,30);
            retryAt=timestamp()+(long)(RecoveryBackoff.Delay(failures).TotalSeconds*frequency);
            throw;
        }
    }
}
