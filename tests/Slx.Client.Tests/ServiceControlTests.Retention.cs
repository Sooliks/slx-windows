using System.Diagnostics;
using Slx.Client.Core;
using Slx.Client.Service;
using Xunit;
namespace Slx.Client.Tests;
public sealed partial class ServiceControlTests
{
    // Long service uptime: a full receipt cache used to refuse every connect until the service restarted.
    [Fact]
    public async Task ExpiredReceiptsFreeCapacityWhileRecentReplayChecksStayStrict()
    {
        long now = 0;
        var engine = new MemoryTunnel();
        var dispatcher = new ControlDispatcher(engine, new Sessions()) { Clock = () => now };
        var first = Request("connect", CoreFixtures.Plan());
        Assert.True((await dispatcher.DispatchAsync("owner", first, default)).Success);
        for (int i = 1; i < ControlDispatcher.ReceiptCapacity; i++)
            Assert.True((await dispatcher.DispatchAsync("owner", Request("reconnect", CoreFixtures.Plan()), default)).Success);
        // Inside the retention window the burst cap and fingerprint checks are unchanged.
        now += Ticks(ControlDispatcher.ReceiptRetention) / 2;
        Assert.Equal("IPC_RECEIPT_LIMIT", (await dispatcher.DispatchAsync("owner", Request("connect", CoreFixtures.Plan()), default)).Code);
        Assert.Equal("IPC_REPLAY_MISMATCH", (await dispatcher.DispatchAsync("owner", first with { Action = "disconnect", Plan = null }, default)).Code);
        var starts = engine.Starts;
        // After the window the old receipts expire: the user can connect again without restarting the service.
        now += Ticks(ControlDispatcher.ReceiptRetention);
        var fresh = Request("connect", CoreFixtures.Plan());
        Assert.True((await dispatcher.DispatchAsync("owner", fresh, default)).Success);
        Assert.Equal(starts + 1, engine.Starts);
        // The new receipt is recent, so its replay is answered from the cache and a changed payload is refused.
        Assert.True((await dispatcher.DispatchAsync("owner", fresh, default)).Success);
        Assert.Equal(starts + 1, engine.Starts);
        Assert.Equal("IPC_REPLAY_MISMATCH", (await dispatcher.DispatchAsync("owner", fresh with { Action = "disconnect", Plan = null }, default)).Code);
        Assert.True((await dispatcher.DispatchAsync("owner", Request("update-applications", CoreFixtures.Plan()), default)).Success);
        Assert.True((await dispatcher.DispatchAsync("owner", Request("disconnect"), default)).Success);
    }
    private static long Ticks(TimeSpan span) => (long)(span.TotalSeconds * Stopwatch.Frequency);
}
