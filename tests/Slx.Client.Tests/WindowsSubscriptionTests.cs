using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Slx.Client.Core;
using Slx.Client.Windows;
using Xunit;

namespace Slx.Client.Tests;

public sealed class WindowsSubscriptionTests
{
    public const string Url = "https://slxvpn.com/sub/000000000000000000000001/synthetic-token-only-for-tests-123456";
    private const string OwnId = "slxw1-0000000000000000000000000000000000000000000000000000000000000001";

    [Theory]
    [InlineData("")]
    [InlineData("http://slxvpn.com/sub/000000000000000000000001/synthetic-token-only-for-tests-123456")]
    [InlineData("https://slxvpn.com.evil.example/sub/000000000000000000000001/synthetic-token-only-for-tests-123456")]
    [InlineData("https://user@slxvpn.com/sub/000000000000000000000001/synthetic-token-only-for-tests-123456")]
    [InlineData("https://slxvpn.com:8443/sub/000000000000000000000001/synthetic-token-only-for-tests-123456")]
    [InlineData("https://127.0.0.1/sub/000000000000000000000001/synthetic-token-only-for-tests-123456")]
    [InlineData("https://slxvpn.com/sub/short/token")]
    public void RejectsNonSlxOrNonHttpsUrlsBeforeAnyHttpRequest(string value) =>
        Assert.Equal("INVALID_URL", Assert.Throws<ClientFault>(() => SubscriptionClient.ValidateUrl(value)).Code);

    [Fact]
    public void RejectsQueryFragmentAndControlCharactersButAllowsCopiedSpaces()
    {
        foreach (var suffix in new[] { "?x-hwid=spoofed", "#fragment", "\n" })
            Assert.Throws<ClientFault>(() => SubscriptionClient.ValidateUrl(Url + suffix));
        Assert.Equal(Url, SubscriptionClient.ValidateUrl(" " + Url + " ").AbsoluteUri);
    }

    [Fact]
    public async Task SuccessfulFetchUsesOnlyOwnIdAndPreservesServerMetadata()
    {
        using var handler = new TestHandler((request, _) =>
        {
            Assert.Equal(Url, request.RequestUri!.AbsoluteUri);
            Assert.Equal(OwnId, request.Headers.GetValues("X-Hwid").Single());
            Assert.Equal("en", request.Headers.AcceptLanguage.Single().Value);
            var response = Response(Encoding.UTF8.GetBytes(CoreFixtures.Hysteria));
            response.Headers.Add("profile-title", "SLX TEST");
            response.Headers.Add("subscription-userinfo", "upload=0; download=0; expire=2000000000");
            return Task.FromResult(response);
        });
        var client = new SubscriptionClient(() => handler, TimeSpan.FromSeconds(2));
        var snapshot = await client.FetchAsync(Url, OwnId, "en");
        Assert.Equal("SLX TEST", snapshot.Title);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(2000000000), snapshot.ExpiresAt);
        Assert.Equal(CoreFixtures.Token, Assert.Single(snapshot.Profiles).Secret);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task CrossOriginRedirectCannotSendInstallationIdToAnotherHost()
    {
        using var handler = new TestHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Redirect)
        {
            Headers = { Location = new Uri("https://other.example/subscription") }
        }));
        var client = new SubscriptionClient(() => handler, TimeSpan.FromSeconds(2));
        Assert.Equal("INVALID_URL", (await Assert.ThrowsAsync<ClientFault>(() => client.FetchAsync(Url, OwnId, "ru"))).Code);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task SameOriginRedirectRemainsBoundedAndCarriesOwnId()
    {
        var calls = 0;
        using var handler = new TestHandler((request, _) =>
        {
            Assert.Equal(OwnId, request.Headers.GetValues("X-Hwid").Single());
            return Task.FromResult(++calls == 1 ? new HttpResponseMessage(HttpStatusCode.Redirect)
            {
                Headers = { Location = new Uri(Url.Replace("123456", "654321")) }
            } : Response(Encoding.UTF8.GetBytes(CoreFixtures.Hysteria)));
        });
        var client = new SubscriptionClient(() => handler, TimeSpan.FromSeconds(2));
        Assert.Single((await client.FetchAsync(Url, OwnId, "ru")).Profiles);
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task DeadlineCoversStalledResponseBodyAfterHeadersAlreadyArrived()
    {
        using var handler = new TestHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(new StalledStream()) { Headers = { ContentType = new MediaTypeHeaderValue("text/plain") } }
        }));
        var client = new SubscriptionClient(() => handler, TimeSpan.FromMilliseconds(150));
        var error = await Assert.ThrowsAsync<ClientFault>(() => client.FetchAsync(Url, OwnId, "ru"));
        Assert.Equal("NETWORK_UNAVAILABLE", error.Code);
    }

    [Fact]
    public async Task CallerCancellationIsPreservedInsteadOfBecomingNetworkFailure()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));
        using var handler = new TestHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(new StalledStream()) { Headers = { ContentType = new MediaTypeHeaderValue("text/plain") } }
        }));
        var client = new SubscriptionClient(() => handler, TimeSpan.FromSeconds(5));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.FetchAsync(Url, OwnId, "ru", cancellation.Token));
    }

    [Theory]
    [InlineData(403, "ACCESS_DENIED")]
    [InlineData(404, "SUBSCRIPTION_UNAVAILABLE")]
    [InlineData(500, "SUBSCRIPTION_UNAVAILABLE")]
    public async Task MapsHttpFailuresToSafeCodes(int status, string expected)
    {
        using var handler = new TestHandler((_, _) => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status)));
        var client = new SubscriptionClient(() => handler, TimeSpan.FromSeconds(2));
        Assert.Equal(expected, (await Assert.ThrowsAsync<ClientFault>(() => client.FetchAsync(Url, OwnId, "ru"))).Code);
    }

    [Theory]
    [InlineData("large")]
    [InlineData("unknown-length")]
    [InlineData("invalid-utf8")]
    [InlineData("html")]
    public async Task InvalidOrOversizeResponsesAreNeverImported(string kind)
    {
        using var handler = new TestHandler((_, _) =>
        {
            var response = kind switch
            {
                "unknown-length" => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StreamContent(new NonSeekableStream(new byte[524289]))
                    { Headers = { ContentType = new MediaTypeHeaderValue("text/plain") } }
                },
                "large" => Response(new byte[524289]),
                "invalid-utf8" => Response([0xff, 0xfe]),
                _ => Response(Encoding.UTF8.GetBytes("<html>not a subscription</html>"))
            };
            if (kind == "html") response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/html");
            return Task.FromResult(response);
        });
        var client = new SubscriptionClient(() => handler, TimeSpan.FromSeconds(2));
        Assert.Equal("INVALID_SUBSCRIPTION", (await Assert.ThrowsAsync<ClientFault>(() => client.FetchAsync(Url, OwnId, "ru"))).Code);
    }

    internal static HttpResponseMessage Response(byte[] body) => new(HttpStatusCode.OK)
    {
        Content = new ByteArrayContent(body) { Headers = { ContentType = new MediaTypeHeaderValue("text/plain") } }
    };
    internal sealed class TestHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        { Calls++; return send(request, cancellationToken); }
    }
    private sealed class StalledStream : MemoryStream
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        { await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken); return 0; }
    }
    private sealed class NonSeekableStream(byte[] data) : MemoryStream(data)
    {
        public override bool CanSeek => false;
    }
}
