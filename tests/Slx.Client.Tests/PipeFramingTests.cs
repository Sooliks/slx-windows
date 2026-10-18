using System.Buffers.Binary;
using System.Text;
using Slx.Client.Core;
using Slx.Client.Windows;
using Xunit;

namespace Slx.Client.Tests;

public sealed class PipeFramingTests
{
    [Fact]
    public async Task FramesRoundTripThroughPartialReads()
    {
        var request = new PipeRequest(PipeContract.Version, "connect", Guid.NewGuid(), CoreFixtures.Plan());
        using var buffer = new MemoryStream();
        await PipeFraming.WriteAsync(buffer, request);
        using var split = new PartialReadStream(buffer.ToArray());
        var actual = await PipeFraming.ReadAsync<PipeRequest>(split);
        Assert.Equal(request.Version, actual.Version);
        Assert.Equal(request.RequestId, actual.RequestId);
        Assert.Equal(request.Plan!.Profile, actual.Plan!.Profile);
        Assert.Equal(request.Plan.Applications, actual.Plan.Applications);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(65537)]
    public async Task RejectsInvalidFrameLengthBeforeReadingBody(int length)
    {
        byte[] header = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(header, length);
        using var stream = new MemoryStream(header);
        var error = await Assert.ThrowsAsync<ClientFault>(() => PipeFraming.ReadAsync<PipeRequest>(stream));
        Assert.Equal("IPC_PAYLOAD_LIMIT", error.Code);
    }

    [Fact]
    public async Task RejectsUnknownFieldsAndTruncatedBodies()
    {
        using var unknown = Frame("{\"Version\":1,\"Action\":\"status\",\"RequestId\":\"00000000-0000-0000-0000-000000000001\",\"Shell\":\"ignored?\"}");
        Assert.Equal("IPC_INVALID", (await Assert.ThrowsAsync<ClientFault>(
            () => PipeFraming.ReadAsync<PipeRequest>(unknown))).Code);
        using var truncated = Frame("{");
        truncated.SetLength(4);
        await Assert.ThrowsAsync<EndOfStreamException>(() => PipeFraming.ReadAsync<PipeRequest>(truncated));
    }

    [Fact]
    public async Task HonorsCancellationWhileReading()
    {
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        using var stream = new MemoryStream(new byte[8]);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => PipeFraming.ReadAsync<PipeRequest>(stream, canceled.Token));
    }

    private static MemoryStream Frame(string payload)
    {
        var bytes = Encoding.UTF8.GetBytes(payload);
        var stream = new MemoryStream();
        byte[] header = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(header, bytes.Length);
        stream.Write(header);
        stream.Write(bytes);
        stream.Position = 0;
        return stream;
    }

    private sealed class PartialReadStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            base.ReadAsync(buffer[..Math.Min(buffer.Length, 3)], cancellationToken);
    }
}
