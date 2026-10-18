using System.IO.Pipes;
using System.Security.Principal;
using Slx.Client.Core;
using Slx.Client.Service;
using Slx.Client.Windows;
using Xunit;

namespace Slx.Client.Tests;

/// <summary>Windows pipe reply delivery, with actual OS scheduling and delayed client reads.</summary>
public sealed class PipeReplyLifecycleTests
{
    [Fact]
    public async Task DelayedReaderReceivesCompleteReplyBeforeServerDisconnects()
    {
        string name = "SLX.Tests." + Guid.NewGuid().ToString("N");
        await using var server = ControlPipe.Create(name);
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var replying = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var expected = new PipeReply(true, "OK", new(TunnelPhase.Disconnected, DateTimeOffset.UtcNow));
        var serving = Task.Run(async () =>
        {
            await server.WaitForConnectionAsync(budget.Token);
            await ControlPipe.ReadRequestAsync(server, budget.Token);
            replying.SetResult();
            await PipeFraming.WriteReplyAndWaitAckAsync(server, expected, budget.Token);
            server.Disconnect();
        });
        await using var client = new NamedPipeClientStream(".", name, PipeDirection.InOut,
            PipeOptions.Asynchronous, TokenImpersonationLevel.Impersonation);
        await client.ConnectAsync(budget.Token);
        await PipeFraming.WriteAsync(client, new PipeRequest(PipeContract.Version, "status", Guid.NewGuid()), budget.Token);
        await replying.Task.WaitAsync(budget.Token);
        await Task.Delay(200, budget.Token);
        var actual = await PipeFraming.ReceiveReplyAndAcknowledgeAsync(client, budget.Token);
        Assert.Equal(expected, actual);
        await serving;
    }
}
