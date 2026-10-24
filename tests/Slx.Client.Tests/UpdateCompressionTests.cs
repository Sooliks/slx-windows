using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text;
using Slx.Client.Core;
using Xunit;

namespace Slx.Client.Tests;

public sealed class UpdateCompressionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompressionCompletesWhenTheIdentityBodyExceedsTheTransferBudget(bool compressed)
    {
        using var fixture = new UpdateFixtures();
        var release = UpdateFixtures.Release("0.1.0-alpha.19");
        release["body"] = string.Concat(Enumerable.Repeat("Verified public release notes.\n", 5000));
        var bytes = Encoding.UTF8.GetBytes(UpdateFixtures.Feed(release));
        Assert.True(bytes.Length > 149771);
        var wire = compressed ? Compress(bytes) : bytes;
        Assert.True(!compressed || wire.Length < 126432);
        using var handler = new UpdateFixtures.Handler((request, _) =>
        {
            if (request.RequestUri!.Host != "api.github.com") return Task.FromResult(UpdateFixtures.Text(UpdateFixtures.Checksums("0.1.0-alpha.19")));
            Assert.Equal("gzip", Assert.Single(request.Headers.AcceptEncoding).Value);
            var response = new HttpResponseMessage(HttpStatusCode.OK)
                { Content = new StreamContent(new CappedTransferStream(wire, 126432)) };
            response.Content.Headers.ContentLength = wire.Length;
            if (compressed) response.Content.Headers.ContentEncoding.Add("gzip");
            return Task.FromResult(response);
        });
        var result = await fixture.Client(handler, timeout: compressed ? TimeSpan.FromSeconds(15) : TimeSpan.FromMilliseconds(100)).CheckAsync("0.1.0-alpha.17");
        Assert.Equal(compressed ? UpdateCheckState.Available : UpdateCheckState.Unavailable, result.State);
        if (compressed) { Assert.Equal("0.1.0-alpha.19", result.Offer!.Version); Assert.Equal(UpdateFixtures.Digest, result.Offer.Sha256); }
        else { Assert.Equal("UPDATE_TIMEOUT", result.ErrorCode); Assert.Null(result.Offer); }
    }
    [Theory]
    [InlineData("gzip")]
    [InlineData("GZIP")]
    [InlineData("identity")]
    public async Task CatalogEncodingDoesNotApplyToChecksums(string encoding)
    {
        using var fixture = new UpdateFixtures();
        var feed = Encoding.UTF8.GetBytes(UpdateFixtures.Feed(UpdateFixtures.Release()));
        using var handler = new UpdateFixtures.Handler((request, _) =>
        {
            if (request.RequestUri!.Host != "api.github.com")
            {
                Assert.Empty(request.Headers.AcceptEncoding);
                return Task.FromResult(UpdateFixtures.Text(UpdateFixtures.Checksums()));
            }
            var response = UpdateFixtures.Response(encoding == "identity" ? feed : Compress(feed));
            response.Content.Headers.ContentEncoding.Add(encoding);
            return Task.FromResult(response);
        });
        Assert.Equal(UpdateCheckState.Available, (await fixture.Client(handler).CheckAsync("0.1.0-alpha.10")).State);
    }
    [Theory]
    [InlineData("decoded-oversize")]
    [InlineData("encoded-oversize")]
    [InlineData("bad-header")]
    [InlineData("bad-crc")]
    [InlineData("bad-size")]
    [InlineData("trim-1")]
    [InlineData("trim-8")]
    [InlineData("trim-9")]
    [InlineData("unknown-encoding")]
    [InlineData("stacked-encoding")]
    [InlineData("concatenated-members")]
    public async Task InvalidOrUnboundedCatalogCannotProduceAnOffer(string kind)
    {
        using var fixture = new UpdateFixtures();
        var feed = Encoding.UTF8.GetBytes(UpdateFixtures.Feed(UpdateFixtures.Release()));
        var bytes = Compress(kind == "decoded-oversize" ? new byte[2097153] : feed);
        if (kind == "bad-header") bytes[0] ^= 0xff;
        if (kind == "bad-crc") bytes[^8] ^= 0xff;
        if (kind == "bad-size") bytes[^4] ^= 0xff;
        if (kind.StartsWith("trim-", StringComparison.Ordinal)) bytes = bytes[..^int.Parse(kind[5..])];
        if (kind == "concatenated-members") bytes = [.. Compress("["u8.ToArray()), .. Compress("{}]"u8.ToArray())];
        using var handler = new UpdateFixtures.Handler((_, _) =>
        {
            var response = kind == "encoded-oversize"
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new UpdateFixtures.UnknownLengthStream(new byte[2097153])) }
                : UpdateFixtures.Response(bytes);
            response.Content.Headers.ContentEncoding.Add(kind == "unknown-encoding" ? "br" : "gzip");
            if (kind == "stacked-encoding") response.Content.Headers.ContentEncoding.Add("gzip");
            return Task.FromResult(response);
        });
        var result = await fixture.Client(handler).CheckAsync("0.1.0-alpha.10");
        Assert.Equal(UpdateCheckState.Unavailable, result.State); Assert.Equal("UPDATE_INVALID", result.ErrorCode);
        Assert.Null(result.Offer); Assert.Equal(1, handler.Calls);
    }
    [Fact]
    public async Task AnEncodedChecksumIsRejectedInsteadOfChangingAssetBytes()
    {
        using var fixture = new UpdateFixtures();
        using var handler = new UpdateFixtures.Handler((request, _) =>
        {
            if (request.RequestUri!.Host == "api.github.com") return Task.FromResult(UpdateFixtures.Text(UpdateFixtures.Feed(UpdateFixtures.Release())));
            Assert.Empty(request.Headers.AcceptEncoding);
            var response = UpdateFixtures.Response(Compress(Encoding.UTF8.GetBytes(UpdateFixtures.Checksums())));
            response.Content.Headers.ContentEncoding.Add("gzip");
            return Task.FromResult(response);
        });
        var result = await fixture.Client(handler).CheckAsync("0.1.0-alpha.10");
        Assert.Equal(UpdateCheckState.Unavailable, result.State); Assert.Equal("UPDATE_INVALID", result.ErrorCode);
    }
    [Fact]
    public async Task GzipBodyDeadlineKeepsCallerCancellationDistinct()
    {
        using var fixture = new UpdateFixtures();
        static Task<HttpResponseMessage> Stalled(HttpRequestMessage _, CancellationToken token)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new UpdateFixtures.StalledStream()) };
            response.Content.Headers.ContentEncoding.Add("gzip");
            return Task.FromResult(response);
        }
        using var handler = new UpdateFixtures.Handler(Stalled);
        var result = await fixture.Client(handler, timeout: TimeSpan.FromMilliseconds(100)).CheckAsync("0.1.0-alpha.10");
        Assert.Equal(UpdateCheckState.Unavailable, result.State); Assert.Equal("UPDATE_TIMEOUT", result.ErrorCode);
        using var cancelledHandler = new UpdateFixtures.Handler(Stalled);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Client(cancelledHandler).CheckAsync("0.1.0-alpha.10", cancellation.Token));
    }
    private static byte[] Compress(byte[] body)
    {
        using var buffer = new MemoryStream();
        using (var gzip = new GZipStream(buffer, CompressionLevel.SmallestSize, true)) gzip.Write(body);
        return buffer.ToArray();
    }
    private sealed class CappedTransferStream(byte[] body, int available) : MemoryStream(body)
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (Position >= Length) return 0;
            if (Position >= available) { await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken); return 0; }
            return await base.ReadAsync(buffer[..(int)Math.Min(buffer.Length, available - Position)], cancellationToken);
        }
    }
}
