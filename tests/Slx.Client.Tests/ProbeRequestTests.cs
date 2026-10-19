using System.Net;
using Slx.Client.Core;
using Slx.Client.Service;
using Xunit;
namespace Slx.Client.Tests;
public sealed class ProbeRequestTests
{
    [Theory]
    [InlineData(ProbeTarget.Gstatic, 204, true)]
    [InlineData(ProbeTarget.Gstatic, 200, false)]
    [InlineData(ProbeTarget.Gstatic, 302, false)]
    [InlineData(ProbeTarget.Slx, 200, true)]
    [InlineData(ProbeTarget.Slx, 503, false)]
    public async Task RequiresExpectedOriginStatus(ProbeTarget target, int status, bool passed)
    {
        using var client = new HttpClient(new Handler((request, _) =>
        {
            Assert.Equal("https", request.RequestUri!.Scheme);
            Assert.Equal(target == ProbeTarget.Gstatic ? "www.gstatic.com" : "slxvpn.com", request.RequestUri.Host);
            Assert.True(request.Headers.CacheControl!.NoCache);
            return Task.FromResult(new HttpResponseMessage((HttpStatusCode)status));
        }));
        var result = await ProbeRequest.ObserveAsync(client, target, default);
        Assert.Equal(passed, result.Passed); Assert.Equal(status, result.HttpStatus);
    }

    [Theory]
    [InlineData(HttpRequestError.ConnectionError, ProbeOutcome.Connection)]
    [InlineData(HttpRequestError.SecureConnectionError, ProbeOutcome.Tls)]
    [InlineData(HttpRequestError.ProxyTunnelError, ProbeOutcome.Proxy)]
    [InlineData(HttpRequestError.NameResolutionError, ProbeOutcome.NameResolution)]
    public async Task KeepsOnlySafeFailureCategory(HttpRequestError error, ProbeOutcome expected)
    {
        using var client = new HttpClient(new Handler((_, _) =>
            throw new HttpRequestException(error, "sensitive diagnostic must not escape")));
        var result = await ProbeRequest.ObserveAsync(client, ProbeTarget.Gstatic, default);
        Assert.Equal(expected, result.Outcome); Assert.Null(result.HttpStatus);
        Assert.DoesNotContain("sensitive", System.Text.Json.JsonSerializer.Serialize(result));
    }

    [Fact]
    public async Task OwnTimeoutIsFailureButCallerCancellationPropagates()
    {
        using var client = new HttpClient(new Handler((_, _) => throw new TaskCanceledException()));
        Assert.Equal(ProbeOutcome.Timeout, (await ProbeRequest.ObserveAsync(client, ProbeTarget.Gstatic, default)).Outcome);
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ProbeRequest.ObserveAsync(client, ProbeTarget.Gstatic, cancel.Token));
    }

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => send(request, cancellationToken);
    }
}
