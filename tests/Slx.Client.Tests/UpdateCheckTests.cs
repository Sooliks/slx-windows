using System.Net;
using System.Net.Http;
using Slx.Client.Core;
using Xunit;

namespace Slx.Client.Tests;

public sealed class UpdateCheckTests
{
    [Fact]
    public async Task OfferRequiresFetchedChecksumAndNeverSendsIdentityOrLaunches()
    {
        using var fixture = new UpdateFixtures();
        using var handler = new UpdateFixtures.Handler((request, _) =>
        {
            Assert.Null(request.Headers.Authorization);
            Assert.False(request.Headers.Contains("X-Hwid")); Assert.False(request.Headers.Contains("Cookie"));
            if (request.RequestUri!.Host == "api.github.com")
                Assert.Equal("gzip", Assert.Single(request.Headers.AcceptEncoding).Value);
            else Assert.Empty(request.Headers.AcceptEncoding);
            return Task.FromResult(request.RequestUri!.Host == "api.github.com"
                ? UpdateFixtures.Text(UpdateFixtures.Feed(UpdateFixtures.Release())) : UpdateFixtures.Text(UpdateFixtures.Checksums()));
        });
        var result = await fixture.Client(handler).CheckAsync("0.1.0-alpha.10");
        Assert.Equal(UpdateCheckState.Available, result.State);
        Assert.Equal(UpdateFixtures.Digest, result.Offer!.Sha256); Assert.Equal(2, handler.Calls);
        Assert.False(Directory.Exists(fixture.Root));
    }
    [Theory]
    [InlineData("checksum-invalid", "UPDATE_INVALID")]
    [InlineData("rate-limit", "UPDATE_UNAVAILABLE")]
    [InlineData("invalid-json", "UPDATE_INVALID")]
    [InlineData("oversize", "UPDATE_INVALID")]
    [InlineData("invalid-utf8", "UPDATE_UNAVAILABLE")]
    public async Task FailureIsASafeUnavailableObservation(string kind, string code)
    {
        using var fixture = new UpdateFixtures();
        using var handler = new UpdateFixtures.Handler((request, _) =>
        {
            if (request.RequestUri!.Host != "api.github.com") return Task.FromResult(UpdateFixtures.Text("invalid checksum"));
            return Task.FromResult(kind switch
            {
                "rate-limit" => new HttpResponseMessage(HttpStatusCode.Forbidden),
                "invalid-json" => UpdateFixtures.Text("private upstream error, not JSON"),
                "oversize" => UpdateFixtures.Response(new byte[2097153]),
                "invalid-utf8" => UpdateFixtures.Response([0xff]),
                _ => UpdateFixtures.Text(UpdateFixtures.Feed(UpdateFixtures.Release()))
            });
        });
        var result = await fixture.Client(handler).CheckAsync("0.1.0-alpha.10");
        Assert.Equal(UpdateCheckState.Unavailable, result.State); Assert.Equal(code, result.ErrorCode); Assert.Null(result.Offer);
    }
    [Theory]
    [InlineData("https://evil.example/setup.exe")]
    [InlineData("http://release-assets.githubusercontent.com/setup.exe")]
    [InlineData("https://user@release-assets.githubusercontent.com/setup.exe")]
    [InlineData("https://github.com/attacker/repo/setup.exe")]
    public async Task RedirectCannotEscapeThePinnedAssetTransport(string target)
    {
        using var fixture = new UpdateFixtures();
        using var handler = new UpdateFixtures.Handler((request, _) => Task.FromResult(request.RequestUri!.Host == "api.github.com"
            ? UpdateFixtures.Text(UpdateFixtures.Feed(UpdateFixtures.Release()))
            : new HttpResponseMessage(HttpStatusCode.Redirect) { Headers = { Location = new Uri(target) } }));
        var result = await fixture.Client(handler).CheckAsync("0.1.0-alpha.10");
        Assert.Equal(UpdateCheckState.Unavailable, result.State); Assert.Equal("UPDATE_INVALID", result.ErrorCode); Assert.Equal(2, handler.Calls);
    }
    [Fact]
    public async Task AssetMayFollowBoundedGitHubCdnRedirects()
    {
        using var fixture = new UpdateFixtures();
        using var handler = new UpdateFixtures.Handler((request, _) => Task.FromResult(request.RequestUri!.Host switch
        {
            "api.github.com" => UpdateFixtures.Text(UpdateFixtures.Feed(UpdateFixtures.Release())),
            "github.com" => new HttpResponseMessage(HttpStatusCode.Redirect) { Headers = { Location = new Uri("https://release-assets.githubusercontent.com/github-production-release-asset/synthetic?signature=fixture") } },
            _ => UpdateFixtures.Text(UpdateFixtures.Checksums())
        }));
        Assert.Equal(UpdateCheckState.Available, (await fixture.Client(handler).CheckAsync("0.1.0-alpha.10")).State);
        Assert.Equal(3, handler.Calls);
    }
    [Fact]
    public async Task DeadlineCoversBodyAndCallerCancellationRemainsCancellation()
    {
        using var fixture = new UpdateFixtures();
        static Task<HttpResponseMessage> Stalled(HttpRequestMessage _, CancellationToken token) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new UpdateFixtures.StalledStream()) });
        using var handler = new UpdateFixtures.Handler(Stalled);
        var result = await fixture.Client(handler, timeout: TimeSpan.FromMilliseconds(100)).CheckAsync("0.1.0-alpha.10");
        Assert.Equal(UpdateCheckState.Unavailable, result.State);
        Assert.Equal("UPDATE_TIMEOUT", result.ErrorCode);
        using var cancelledHandler = new UpdateFixtures.Handler(Stalled);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Client(cancelledHandler).CheckAsync("0.1.0-alpha.10", cancellation.Token));
    }
}
