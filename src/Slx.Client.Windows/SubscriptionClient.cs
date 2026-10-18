using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using Slx.Client.Core;

namespace Slx.Client.Windows;

public sealed partial class SubscriptionClient
{
    private const int MaxResponse = 524288;
    private readonly Func<HttpMessageHandler> createHandler;
    private readonly TimeSpan timeout;
    public SubscriptionClient() : this(() => new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false }, TimeSpan.FromSeconds(15)) { }
    internal SubscriptionClient(Func<HttpMessageHandler> handlerFactory, TimeSpan deadline)
    {
        createHandler = handlerFactory;
        timeout = deadline;
    }
    [GeneratedRegex("\\A/sub/[a-f0-9]{24}/[A-Za-z0-9_-]{30,128}\\z", RegexOptions.CultureInvariant)]
    private static partial Regex SubscriptionPath();
    public static Uri ValidateUrl(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw.Length > 1024 || raw.Any(char.IsControl)
            || !Uri.TryCreate(raw.Trim(), UriKind.Absolute, out var uri)
            || uri.Scheme != "https" || !uri.IsDefaultPort || uri.Host != "slxvpn.com"
            || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0
            || !SubscriptionPath().IsMatch(uri.AbsolutePath)) throw new ClientFault("INVALID_URL");
        return uri;
    }
    public async Task<SubscriptionSnapshot> FetchAsync(string url, string hwid, string language, CancellationToken cancellationToken = default)
    {
        var uri = ValidateUrl(url);
        if (string.IsNullOrEmpty(hwid) || hwid.Length is < 8 or > 256
            || hwid.Any(c => c < 33 || c > 126 || c == ',')) throw new ClientFault("INVALID_HWID");
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(timeout);
        using var client = new HttpClient(createHandler()) { Timeout = Timeout.InfiniteTimeSpan };
        try
        {
            for (var redirects = 0; redirects < 3; redirects++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                request.Headers.TryAddWithoutValidation("X-Hwid", hwid);
                request.Headers.UserAgent.ParseAdd("SLX-Windows/0.1.0");
                request.Headers.AcceptLanguage.ParseAdd(language == "en" ? "en" : "ru");
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, budget.Token);
                if ((int)response.StatusCode is >= 300 and < 400)
                {
                    if (response.Headers.Location is null) throw new ClientFault("SUBSCRIPTION_UNAVAILABLE");
                    uri = ValidateUrl(new Uri(uri, response.Headers.Location).AbsoluteUri); continue;
                }
                if (response.StatusCode == HttpStatusCode.Forbidden) throw new ClientFault("ACCESS_DENIED");
                if (response.StatusCode == HttpStatusCode.NotFound) throw new ClientFault("SUBSCRIPTION_UNAVAILABLE");
                if (!response.IsSuccessStatusCode) throw new ClientFault("SUBSCRIPTION_UNAVAILABLE");
                if (response.Content.Headers.ContentLength > MaxResponse) throw new ClientFault("INVALID_SUBSCRIPTION");
                var media = response.Content.Headers.ContentType?.MediaType;
                if (media is not ("text/plain" or "application/octet-stream")) throw new ClientFault("INVALID_SUBSCRIPTION");
                using var buffer = new MemoryStream();
                using var stream = await response.Content.ReadAsStreamAsync(budget.Token);
                var chunk = new byte[8192]; int count;
                while ((count = await stream.ReadAsync(chunk, budget.Token)) > 0)
                { if (buffer.Length + count > MaxResponse) throw new ClientFault("INVALID_SUBSCRIPTION"); buffer.Write(chunk, 0, count); }
                string body;
                try { body = new UTF8Encoding(false, true).GetString(buffer.GetBuffer(), 0, checked((int)buffer.Length)); }
                catch (DecoderFallbackException) { throw new ClientFault("INVALID_SUBSCRIPTION"); }
                var title = response.Headers.TryGetValues("profile-title", out var titles) ? titles.FirstOrDefault() : null;
                DateTimeOffset? expires = null;
                if (response.Headers.TryGetValues("subscription-userinfo", out var info))
                {
                    foreach (var segment in info.First().Split(';'))
                        if (segment.Trim().StartsWith("expire=", StringComparison.Ordinal) && long.TryParse(segment.Trim()[7..], out var unix))
                        { try { expires = DateTimeOffset.FromUnixTimeSeconds(unix); } catch (ArgumentOutOfRangeException) { throw new ClientFault("INVALID_SUBSCRIPTION"); } }
                }
                return SubscriptionParser.Parse(body, title, expires);
            }
            throw new ClientFault("SUBSCRIPTION_UNAVAILABLE");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { throw new ClientFault("NETWORK_UNAVAILABLE"); }
        catch (HttpRequestException) { throw new ClientFault("NETWORK_UNAVAILABLE"); }
        catch (IOException) { throw new ClientFault("NETWORK_UNAVAILABLE"); }
    }
}
