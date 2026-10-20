using System.Net;
using System.Net.Http;
using Slx.Client.Core;

namespace Slx.Client.Windows;

internal sealed class UpdateTransport(HttpMessageHandler handler) : IDisposable
{
    private readonly HttpClient client = new(handler) { Timeout = Timeout.InfiniteTimeSpan };
    public void Dispose() => client.Dispose();
    public async Task<byte[]> ReadAsync(Uri uri, int maximum, long? expected, bool asset, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await CopyAsync(uri, buffer, maximum, expected, asset, null, cancellationToken);
        return buffer.ToArray();
    }
    public async Task CopyAsync(Uri original, Stream output, long maximum, long? expected, bool asset,
        IProgress<int>? progress, CancellationToken cancellationToken)
    {
        var uri = original;
        for (var redirect = 0; redirect < 4; redirect++)
        {
            if (!Allowed(uri, original, asset)) throw new ClientFault("UPDATE_INVALID");
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.UserAgent.ParseAdd("SLX-Windows-Update");
            request.Headers.Accept.ParseAdd(asset ? "application/octet-stream" : "application/vnd.github+json");
            if (!asset) request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if ((int)response.StatusCode is >= 300 and < 400)
            {
                if (!asset || response.Headers.Location is null) throw new ClientFault("UPDATE_INVALID");
                uri = new Uri(uri, response.Headers.Location); continue;
            }
            if (response.StatusCode != HttpStatusCode.OK) throw new ClientFault("UPDATE_UNAVAILABLE");
            if (response.Content.Headers.ContentLength is long length && (length > maximum || (expected.HasValue && length != expected)))
                throw new ClientFault("UPDATE_INVALID");
            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var chunk = new byte[65536]; long received = 0; var reported = -1; int count;
            while ((count = await stream.ReadAsync(chunk, cancellationToken)) > 0)
            {
                received += count;
                if (received > maximum || (expected.HasValue && received > expected)) throw new ClientFault("UPDATE_INVALID");
                await output.WriteAsync(chunk.AsMemory(0, count), cancellationToken);
                var percent = expected is > 0 ? (int)(received * 100 / expected.Value) : 0;
                if (percent != reported) { progress?.Report(percent); reported = percent; }
            }
            if (received == 0 || (expected.HasValue && received != expected)) throw new ClientFault("UPDATE_INVALID");
            return;
        }
        throw new ClientFault("UPDATE_INVALID");
    }
    private static bool Allowed(Uri uri, Uri original, bool asset)
    {
        if (!UpdateSourcePolicy.Secure(uri) || uri.AbsoluteUri.Length > 8192) return false;
        if (!asset) return uri.AbsoluteUri == UpdateSourcePolicy.Feed;
        if (uri.Host == "github.com") return uri == original && uri.Query.Length == 0;
        return uri.Host is "release-assets.githubusercontent.com" or "objects.githubusercontent.com";
    }
}
