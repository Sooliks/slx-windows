using Slx.Client.Core;
using Slx.Client.Service;
using Xunit;
namespace Slx.Client.Tests;
/// <summary>Real startup cleanup scheduler with injected time/release port, no WFP or service mutation.</summary>
public sealed class StartupOffCleanupTests
{
    [Fact]
    public void NoKnownOffIntentMeansNoCleanupCanRun()
    {
        var cleanup=new StartupOffCleanup(()=>0,1000);int calls=0;
        Assert.False(cleanup.TryRelease(()=>calls++));Assert.False(cleanup.Pending);Assert.Equal(0,calls);
    }
    [Fact]
    public void TransientBfeFailureRetainsPendingAndRetriesWithPacedBackoffUntilConfirmedRelease()
    {
        long now=0;int calls=0;
        var cleanup=new StartupOffCleanup(()=>now,1000);cleanup.Begin();
        void Failing(){calls++;throw new ClientFault("TRAFFIC_PROTECTION_OPEN_000006BA");}
        Assert.Throws<ClientFault>(()=>cleanup.TryRelease(Failing));Assert.True(cleanup.Pending);
        now=1999;Assert.False(cleanup.TryRelease(Failing));Assert.Equal(1,calls);
        now=2000;Assert.Throws<ClientFault>(()=>cleanup.TryRelease(Failing));Assert.Equal(2,calls);
        now=6999;Assert.False(cleanup.TryRelease(Failing));Assert.Equal(2,calls);
        now=7000;Assert.True(cleanup.TryRelease(()=>calls++));
        Assert.False(cleanup.Pending);Assert.Equal(3,calls);
        now=1000000;Assert.False(cleanup.TryRelease(()=>calls++));Assert.Equal(3,calls);
        Assert.Equal("SERVICE_STARTUP_CLEANUP_PENDING",StartupOffCleanup.PendingCode);
    }
    [Fact]
    public void NewConnectionOrExplicitStopCancelsAStartupReleaseBeforeItCanRemoveNewPolicy()
    {
        long now=0;int calls=0;
        var cleanup=new StartupOffCleanup(()=>now,1000);cleanup.Begin();
        Assert.Throws<ClientFault>(()=>cleanup.TryRelease(()=>throw new ClientFault("TRAFFIC_PROTECTION_BEGIN_80320012")));
        cleanup.Cancel();now=1000000;
        Assert.False(cleanup.TryRelease(()=>calls++));Assert.Equal(0,calls);Assert.False(cleanup.Pending);
    }
    [Fact]
    public void RepeatedFailuresKeepBoundedPacingWithoutExhaustingTheOffRestore()
    {
        long now=0;var cleanup=new StartupOffCleanup(()=>now,1000);cleanup.Begin();
        foreach(int seconds in new[]{2,5,10,20,40,60,60,60})
        {
            Assert.Throws<ClientFault>(()=>cleanup.TryRelease(()=>throw new ClientFault("TRAFFIC_PROTECTION_OPEN_000006BA")));
            Assert.True(cleanup.Pending);
            now+=seconds*1000-1;Assert.False(cleanup.TryRelease(()=>throw new InvalidOperationException("Retry happened early.")));now++;
        }
        Assert.True(cleanup.TryRelease(()=>{}));Assert.False(cleanup.Pending);
    }
}
