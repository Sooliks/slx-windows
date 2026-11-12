using System.Net;
using System.Net.Sockets;
using Slx.Client.Core;

namespace Slx.Client.Service;

internal readonly record struct EstablishedObservation(ProbeObservation Observation, bool Reused);

/// <summary>
/// Keeps one connection open through the VPN core and reports whether a request on it was answered over that
/// same connection. The callback also counts transparent reconnects made by HttpClient.
/// </summary>
internal sealed class EstablishedProbe : IDisposable
{
    private readonly HttpClient client;
    private readonly Func<ProbeTarget, string> address;
    private int connections;
    private bool opened;

    internal EstablishedProbe(int port, string user, string password)
        : this(Handler(new WebProxy("http://127.0.0.1:" + port) { Credentials = new NetworkCredential(user, password) }), DefaultAddress) { }

    internal EstablishedProbe(SocketsHttpHandler handler, Func<ProbeTarget, string> address)
    {
        this.address = address;
        handler.PooledConnectionLifetime = Timeout.InfiniteTimeSpan;
        handler.PooledConnectionIdleTimeout = TimeSpan.FromMinutes(10);
        handler.MaxConnectionsPerServer = 1;
        handler.ConnectCallback = async (context, token) =>
        {
            Interlocked.Increment(ref connections);
            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try { await socket.ConnectAsync(context.DnsEndPoint, token); return new NetworkStream(socket, ownsSocket: true); }
            catch { socket.Dispose(); throw; }
        };
        client = new HttpClient(handler, disposeHandler: true) { Timeout = TimeSpan.FromSeconds(5) };
    }

    internal static SocketsHttpHandler Handler(IWebProxy? proxy) => new()
    {
        Proxy = proxy, UseProxy = proxy is not null, AllowAutoRedirect = false, ConnectTimeout = TimeSpan.FromSeconds(3)
    };

    private static string DefaultAddress(ProbeTarget target) => target == ProbeTarget.Gstatic
        ? "https://www.gstatic.com/generate_204" : "https://slxvpn.com/robots.txt";

    internal async Task<EstablishedObservation> ObserveAsync(CancellationToken cancellationToken)
    {
        int before = Volatile.Read(ref connections);
        var observation = await ProbeRequest.ObserveAsync(client, ProbeTarget.Gstatic, cancellationToken, address);
        // Reused only if a connection existed before this request and the request did not have to open another.
        bool reused = opened && Volatile.Read(ref connections) == before;
        opened |= observation.Passed;
        return new(observation, reused);
    }

    public void Dispose() => client.Dispose();
}
