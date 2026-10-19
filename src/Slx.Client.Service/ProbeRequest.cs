using System.Diagnostics;
using System.Net;
using Slx.Client.Core;
namespace Slx.Client.Service;
internal static class ProbeRequest
{
    internal static async Task<ProbeObservation> ObserveAsync(HttpClient client, ProbeTarget target,
        CancellationToken cancellationToken)
    {
        var clock = Stopwatch.StartNew();
        try
        {
            string url = target == ProbeTarget.Gstatic
                ? "https://www.gstatic.com/generate_204" : "https://slxvpn.com/robots.txt";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.CacheControl = new() { NoCache = true, NoStore = true };
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            var expected = target == ProbeTarget.Gstatic ? HttpStatusCode.NoContent : HttpStatusCode.OK;
            return new(target, response.StatusCode == expected ? ProbeOutcome.Success : ProbeOutcome.HttpRejected,
                (int)clock.ElapsedMilliseconds, (int)response.StatusCode);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { return new(target, ProbeOutcome.Timeout, (int)clock.ElapsedMilliseconds); }
        catch (HttpRequestException error)
        {
            var outcome = error.HttpRequestError switch
            {
                HttpRequestError.NameResolutionError => ProbeOutcome.NameResolution,
                HttpRequestError.ConnectionError => ProbeOutcome.Connection,
                HttpRequestError.SecureConnectionError => ProbeOutcome.Tls,
                HttpRequestError.ProxyTunnelError => ProbeOutcome.Proxy,
                HttpRequestError.HttpProtocolError or HttpRequestError.InvalidResponse
                    or HttpRequestError.ResponseEnded => ProbeOutcome.Protocol,
                _ => ProbeOutcome.Other
            };
            return new(target, outcome, (int)clock.ElapsedMilliseconds);
        }
    }
}
