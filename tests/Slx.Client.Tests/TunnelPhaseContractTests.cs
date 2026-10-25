using Slx.Client.Core;
using Slx.Client.Windows;
using Xunit;
namespace Slx.Client.Tests;

public sealed class TunnelPhaseContractTests
{
    [Fact]
    public void CheckingIsAppendedWithoutChangingExistingWireValues()
    {
        Assert.Equal(new[] { 0, 1, 2, 3, 4, 5, 6 }, new[]
        {
            TunnelPhase.Disconnected, TunnelPhase.Connecting, TunnelPhase.Connected,
            TunnelPhase.Reconnecting, TunnelPhase.Stopping, TunnelPhase.Faulted, TunnelPhase.Checking
        }.Select(phase => (int)phase));
    }

    [Fact]
    public async Task CheckingRoundTripsThroughTheStrictPipeWithoutClaimingReadinessOrOff()
    {
        var status = new TunnelStatus(TunnelPhase.Checking, DateTimeOffset.UtcNow,
            ConnectedAt: DateTimeOffset.UtcNow.AddMinutes(-1), ProbePassed: false, ProtectionActive: true);
        var reply = new PipeReply(true, "OK", status);
        using var stream = new MemoryStream();
        await PipeFraming.WriteAsync(stream, reply);
        stream.Position = 0;
        var observed = await PipeFraming.ReadAsync<PipeReply>(stream);
        Assert.Equal(reply, observed);
        Assert.Equal(TunnelPhase.Checking, observed.Status.Phase);
        Assert.False(observed.Status.ProbePassed); Assert.True(observed.Status.ProtectionActive);
    }
}
