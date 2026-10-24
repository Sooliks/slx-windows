using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Slx.Client.Core;
using Slx.Client.Windows;

namespace Slx.Client.Service;

internal static class TunnelDnsProbe
{
    private static readonly IPAddress Destination = IPAddress.Parse("1.1.1.1");

    public static Task CheckAsync(string ownedAdapterId, Action<DnsCheckObservation> observe,
        CancellationToken cancellationToken) => CheckAsync(ExchangeAsync,
            () => TunnelDnsRoute.Observe(ownedAdapterId, Destination), observe, cancellationToken);

    internal static async Task CheckAsync(Func<byte[], CancellationToken, Task<byte[]>> exchange,
        Func<DnsRouteObservation> route, Action<DnsCheckObservation> observe, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(TimeSpan.FromSeconds(9));
        var attempts = new DnsProbeObservation?[3];
        for (int attempt = 0; attempt < attempts.Length; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (budget.IsCancellationRequested) break;
            var target = attempt % 2 == 0 ? ProbeTarget.Gstatic : ProbeTarget.Slx;
            var clock = Stopwatch.StartNew();
            var observedRoute = route();
            cancellationToken.ThrowIfCancellationRequested();
            var outcome = RouteOutcome(observedRoute);
            int? responseCode = null, socketError = null;
            if (outcome == DnsProbeOutcome.Success)
            {
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(budget.Token);
                deadline.CancelAfter(TimeSpan.FromSeconds(3));
                try
                {
                    deadline.Token.ThrowIfCancellationRequested();
                    var query = DnsProbePacket.CreateQuery(target);
                    var response = await exchange(query, deadline.Token);
                    deadline.Token.ThrowIfCancellationRequested();
                    (outcome, responseCode) = DnsProbePacket.Evaluate(query, response);
                    if (outcome == DnsProbeOutcome.Success)
                    {
                        // Keep the ordinary OS route: binding only the probe could hide broken shared DNS.
                        observedRoute = route();
                        outcome = RouteOutcome(observedRoute);
                        deadline.Token.ThrowIfCancellationRequested();
                    }
                }
                catch (SocketException error)
                { outcome = DnsProbeOutcome.SocketError; socketError = (int)error.SocketErrorCode; }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                { outcome = DnsProbeOutcome.Timeout; }
            }
            cancellationToken.ThrowIfCancellationRequested();
            attempts[attempt] = new(target, outcome, (int)clock.ElapsedMilliseconds,
                responseCode, socketError, observedRoute);
            observe(new(DateTimeOffset.UtcNow, attempts[0]!, attempts[1], attempts[2]));
            cancellationToken.ThrowIfCancellationRequested();
            if (outcome == DnsProbeOutcome.Success) return;
        }
        cancellationToken.ThrowIfCancellationRequested();
        throw new ClientFault("VPN_DNS_FAILED");
    }

    private static DnsProbeOutcome RouteOutcome(DnsRouteObservation route) => route.Status switch
    {
        DnsRouteStatus.OwnedTunnel => DnsProbeOutcome.Success,
        DnsRouteStatus.OtherInterface => DnsProbeOutcome.RouteMismatch,
        _ => DnsProbeOutcome.RouteUnavailable
    };

    private static async Task<byte[]> ExchangeAsync(byte[] query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var udp = new UdpClient(AddressFamily.InterNetwork);
        udp.Connect(Destination, 53);
        await udp.SendAsync(query, cancellationToken);
        return (await udp.ReceiveAsync(cancellationToken)).Buffer;
    }
}
