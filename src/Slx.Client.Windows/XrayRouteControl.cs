using System.Net;
using System.Net.Sockets;
using System.Text;
using Slx.Client.Core;
namespace Slx.Client.Windows;
/// <summary>Authenticated in-memory RoutingService calls through this core's existing probe inbound.</summary>
public sealed partial class XrayRouteControl
{
    private readonly int port;
    private readonly string user, password;
    public XrayRouteControl(int port, string user, string password)
    {
        if (port is < 1024 or > 65535 || !Safe(user) || !Safe(password)) throw new ClientFault("INVALID_PLAN");
        this.port = port; this.user = user; this.password = password;
    }
    public async Task ReplaceAsync(ConnectPlan pinnedPlan, CancellationToken cancellationToken)
    {
        var response = await CallAsync("AddRule", XrayRouteProtocol.BuildReplacement(pinnedPlan), cancellationToken).ConfigureAwait(false);
        if (response.Length != 0) throw new ClientFault("ROUTING_INVALID_REPLY");
    }
    public async Task<string> ReadRevisionAsync(CancellationToken cancellationToken)
        => ReadRevision(await CallAsync("ListRule", [], cancellationToken).ConfigureAwait(false));
    private async Task<byte[]> CallAsync(string method, byte[] body, CancellationToken cancellationToken)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(TimeSpan.FromSeconds(5)); int connected = 0;
        using var handler = new SocketsHttpHandler
        {
            UseProxy = false, AllowAutoRedirect = false, UseCookies = false, AutomaticDecompression = DecompressionMethods.None,
            MaxResponseHeadersLength = 8, ConnectTimeout = TimeSpan.FromSeconds(3), ActivityHeadersPropagator = null,
            ConnectCallback = async (context, token) =>
            {
                if (context.DnsEndPoint.Host != XrayRouteProtocol.Host || context.DnsEndPoint.Port != 80
                    || Interlocked.Exchange(ref connected, 1) != 0) throw new ClientFault("ROUTING_UNAVAILABLE");
                return await OpenTunnelAsync(token).ConfigureAwait(false);
            }
        };
        using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        using var request = new HttpRequestMessage(HttpMethod.Post, "http://" + XrayRouteProtocol.Host + "/" + XrayRouteProtocol.Service + "/" + method)
        {
            Version = HttpVersion.Version20, VersionPolicy = HttpVersionPolicy.RequestVersionExact,
            Content = RouteSingleUseContent.Create(body)
        };
        request.Headers.TryAddWithoutValidation("TE", "trailers"); request.Headers.TryAddWithoutValidation("grpc-timeout", "5S");
        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, budget.Token).ConfigureAwait(false);
            return await RouteGrpcMessage.ReadAsync(response, budget.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { throw new ClientFault("ROUTING_TIMEOUT"); }
        catch (Exception error) when (error is HttpRequestException or IOException or SocketException) { throw new ClientFault("ROUTING_UNAVAILABLE"); }
    }
    private async ValueTask<Stream> OpenTunnelAsync(CancellationToken token)
    {
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(IPAddress.Loopback, port, token).ConfigureAwait(false);
            var stream = new NetworkStream(socket, ownsSocket: true);
            string auth = Convert.ToBase64String(Encoding.ASCII.GetBytes(user + ":" + password));
            byte[] request = Encoding.ASCII.GetBytes("CONNECT " + XrayRouteProtocol.Host + ":80 HTTP/1.1\r\nHost: "
                + XrayRouteProtocol.Host + ":80\r\nProxy-Authorization: Basic " + auth + "\r\n\r\n");
            await stream.WriteAsync(request, token).ConfigureAwait(false);
            var header = new List<byte>(); var one = new byte[1];
            while (header.Count < 4096)
            {
                if (await stream.ReadAsync(one, token).ConfigureAwait(false) != 1) throw new ClientFault("ROUTING_UNAVAILABLE");
                header.Add(one[0]);
                if (header.Count < 4 || !header.TakeLast(4).SequenceEqual(new byte[] { 13, 10, 13, 10 })) continue;
                if (!Encoding.ASCII.GetString(header.ToArray()).StartsWith("HTTP/1.1 200 ", StringComparison.Ordinal)) throw new ClientFault("ROUTING_UNAVAILABLE");
                return stream;
            }
            throw new ClientFault("ROUTING_INVALID_REPLY");
        }
        catch { socket.Dispose(); throw; }
    }
    private static bool Safe(string value) => value is { Length: >= 16 and <= 128 }
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '=' or '+');
}
