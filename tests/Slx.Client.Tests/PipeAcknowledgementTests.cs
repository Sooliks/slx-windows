using System.IO.Pipes;
using System.Security.Principal;
using Slx.Client.Core;
using Slx.Client.Service;
using Slx.Client.Windows;
using Xunit;

namespace Slx.Client.Tests;

public sealed class PipeAcknowledgementTests
{
    [Fact]
    public async Task ThirtyDelayedReadsSurvivePersistentPipeReconnects()
    {
        string name = "SLX.Tests." + Guid.NewGuid().ToString("N");
        await using var server = ControlPipe.Create(name);
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var expected = new PipeReply(true, "OK", new(TunnelPhase.Disconnected, DateTimeOffset.UtcNow));
        var serving = Task.Run(async () =>
        {
            for (int index = 0; index < 30; index++)
            {
                await server.WaitForConnectionAsync(budget.Token);
                await ControlPipe.ReadRequestAsync(server, budget.Token);
                Assert.True(await PipeFraming.WriteReplyAndWaitAckAsync(server, expected, budget.Token));
                server.Disconnect();
            }
        });
        for (int index = 0; index < 30; index++)
        {
            await using var client = Client(name);
            await client.ConnectAsync(budget.Token);
            await PipeFraming.WriteAsync(client, Request(), budget.Token);
            await Task.Delay(index % 4 * 10, budget.Token);
            Assert.Equal(expected, await PipeFraming.ReceiveReplyAndAcknowledgeAsync(client, budget.Token));
        }
        await serving;
    }

    [Fact]
    public async Task MissingAcknowledgementHasABoundedDeadline()
    {
        string name = "SLX.Tests." + Guid.NewGuid().ToString("N");
        await using var server = ControlPipe.Create(name);
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(6));
        var serving = Task.Run(async () =>
        {
            await server.WaitForConnectionAsync(budget.Token);
            await ControlPipe.ReadRequestAsync(server, budget.Token);
            bool acknowledged = await PipeFraming.WriteReplyAndWaitAckAsync(server,
                new(true, "OK", new(TunnelPhase.Disconnected, DateTimeOffset.UtcNow)), budget.Token);
            server.Disconnect();
            return acknowledged;
        });
        await using var client = Client(name);
        await client.ConnectAsync(budget.Token);
        await PipeFraming.WriteAsync(client, Request(), budget.Token);
        Assert.True((await PipeFraming.ReadAsync<PipeReply>(client, budget.Token)).Success);
        Assert.False(await serving.WaitAsync(TimeSpan.FromSeconds(4)));
    }

    [Fact]
    public async Task FailedAckWritePreservesTheCompleteKnownReply()
    {
        string name = "SLX.Tests." + Guid.NewGuid().ToString("N");
        await using var server = ControlPipe.Create(name);
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var expected = new PipeReply(true, "OK", new(TunnelPhase.Disconnected, DateTimeOffset.UtcNow));
        var serving = Task.Run(async () =>
        {
            await server.WaitForConnectionAsync(budget.Token);
            await ControlPipe.ReadRequestAsync(server, budget.Token);
            return await PipeFraming.WriteReplyAndWaitAckAsync(server, expected, budget.Token);
        });
        await using var client = Client(name);
        await client.ConnectAsync(budget.Token);
        await PipeFraming.WriteAsync(client, Request(), budget.Token);
        using var failingAck = new AckFailureStream(client);
        Assert.Equal(expected, await PipeFraming.ReceiveReplyAndAcknowledgeAsync(failingAck, budget.Token));
        await client.DisposeAsync();
        Assert.False(await serving);
    }

    private static PipeRequest Request() => new(PipeContract.Version, "status", Guid.NewGuid());
    private static NamedPipeClientStream Client(string name) => new(".", name, PipeDirection.InOut,
        PipeOptions.Asynchronous, TokenImpersonationLevel.Impersonation);

    private sealed class AckFailureStream(Stream source) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => source.Read(buffer, offset, count);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            source.ReadAsync(buffer, cancellationToken);
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
            ValueTask.FromException(new IOException("Synthetic ACK write failure after real pipe reply"));
        public override void Write(byte[] buffer, int offset, int count) => throw new IOException("Synthetic ACK failure");
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
