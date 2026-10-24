using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using Slx.Client.Core;

namespace Slx.Client.Service;

internal static class LocalProbe
{
    public static (int Port, string User, string Password) ReserveCredentials()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return (((IPEndPoint)listener.LocalEndpoint).Port, Convert.ToHexString(RandomNumberGenerator.GetBytes(16)),
            Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
    }

    public static async Task WaitAsync(int port, string user, string password,
        Func<bool> childAlive, CancellationToken cancellationToken)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(TimeSpan.FromSeconds(32));
        while (!budget.IsCancellationRequested)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!childAlive()) throw new ClientFault("CORE_EXITED");
            try { if (await CheckOnceAsync(port, user, password, budget.Token) && TunnelReady()) return; }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { break; }
            try { await Task.Delay(500, budget.Token); }
            catch (OperationCanceledException) { break; }
        }
        cancellationToken.ThrowIfCancellationRequested();
        throw new ClientFault("VPN_PROBE_FAILED");
    }

    public static async Task<bool> CheckOnceAsync(int port, string user, string password, CancellationToken cancellationToken)
        => await StartupReadiness.CheckAsync((target, token) => ObserveAsync(port, user, password, target, token), cancellationToken);

    public static async Task<ProbeObservation> ObserveAsync(int port, string user, string password,
        ProbeTarget target, CancellationToken cancellationToken)
    {
        using var handler = new SocketsHttpHandler
        {
            Proxy = new WebProxy("http://127.0.0.1:" + port)
            { Credentials = new NetworkCredential(user, password) },
            UseProxy = true, AllowAutoRedirect = false, ConnectTimeout = TimeSpan.FromSeconds(3)
        };
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };
        return await ProbeRequest.ObserveAsync(client, target, cancellationToken);
    }
    public static string? AdapterIdentity() => NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(adapter => adapter.Name == "SLX VPN" && PresentAdapters.Contains(adapter.Id))?.Id;
    public static bool AdapterIdentityExists(string id) => PresentAdapters.Contains(id);

    public static bool AdapterExists() => AdapterIdentity() is not null;

    public static bool TunnelReady() => NetworkInterface.GetAllNetworkInterfaces().Any(adapter =>
        adapter.Name == "SLX VPN" && adapter.OperationalStatus == OperationalStatus.Up
        && new[] { IPAddress.Parse("172.30.255.1"), IPAddress.Parse("fd73:6c78::1") }.All(expected =>
            adapter.GetIPProperties().UnicastAddresses.Any(address => address.Address.Equals(expected)
                && address.DuplicateAddressDetectionState == DuplicateAddressDetectionState.Preferred)));
}
