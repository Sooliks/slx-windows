using Slx.Client.Core;
namespace Slx.Client.Service;
internal sealed partial class TunnelEngine
{
    private volatile RecoveryObservation? recovery;
    internal void RecordRecovery(RecoveryCause cause)
    {
        var count=Math.Min(recovery?.Count??0,int.MaxValue-1)+1;
        recovery=new(count,cause,DateTimeOffset.UtcNow);
    }
}
