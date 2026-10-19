using System.Text.Json;
using Slx.Client.Core;
using Slx.Client.Service;
using Xunit;
namespace Slx.Client.Tests;
public sealed class RecoveryObservationTests
{
    [Fact]
    public void OldStatusWithoutDiagnosticsRemainsReadable()
    {
        const string old="{\"Phase\":2,\"ObservedAt\":\"2026-10-19T13:00:00Z\",\"ProbePassed\":true}";
        var status=JsonSerializer.Deserialize<TunnelStatus>(old)!;
        Assert.Equal(TunnelPhase.Connected,status.Phase);Assert.True(status.ProbePassed);Assert.Null(status.Recovery);Assert.Null(status.HealthCheck);
    }
    [Fact]
    public void RecoveryReasonSurvivesStatusReadsWithoutClaimingConnectionSuccess()
    {
        var engine=new TunnelEngine();var before=DateTimeOffset.UtcNow;
        engine.RecordRecovery(RecoveryCause.ProbeFailed);
        engine.RecordRecovery(RecoveryCause.CoreExited);
        var status=engine.Status;
        Assert.Equal(TunnelPhase.Disconnected,status.Phase);Assert.False(status.ProbePassed);
        Assert.Null(status.ConnectedAt);Assert.Equal(2,status.Recovery!.Count);
        Assert.Equal(RecoveryCause.CoreExited,status.Recovery.Cause);
        Assert.InRange(status.Recovery.At,before,DateTimeOffset.UtcNow);
        Assert.Equal(status.Recovery,engine.Status.Recovery);
        Assert.Equal(status,JsonSerializer.Deserialize<TunnelStatus>(JsonSerializer.Serialize(status)));
    }
}
