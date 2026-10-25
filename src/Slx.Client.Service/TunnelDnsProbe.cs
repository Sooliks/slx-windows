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
        Func<DnsRouteObservation> route, Action<DnsCheckObservation> observe, CancellationToken cancellationToken,
        Func<TimeSpan, CancellationToken, Task>? pause = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        pause ??= Task.Delay;
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(TimeSpan.FromSeconds(9));
        var attempts = new DnsProbeObservation?[3];
        for (int attempt = 0; attempt < attempts.Length; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (budget.IsCancellationRequested) break;
            var target = attempt % 2 == 0 ? ProbeTarget.Gstatic : ProbeTarget.Slx;
            var clock = Stopwatch.StartNew();
            var observedRoute = new DnsRouteObservation(DnsRouteStatus.Unavailable);
            var outcome = DnsProbeOutcome.RouteUnavailable;
            int? responseCode = null, socketError = null;
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(budget.Token);
            deadline.CancelAfter(TimeSpan.FromSeconds(3));
            try
            {
                // Preferred addresses and an authenticated HTTP proxy do not prove that Windows
                // has selected the TUN route yet. Give the ordinary route time to settle, without
                // sending DNS over a different interface or extending the existing attempt budget.
                for (int sample = 0; sample < 6; sample++)
                {
                    deadline.Token.ThrowIfCancellationRequested();
                    var candidate = route();
                    deadline.Token.ThrowIfCancellationRequested();
                    observedRoute = candidate;
                    outcome = RouteOutcome(observedRoute);
                    if (outcome == DnsProbeOutcome.Success) break;
                    if (sample < 5)
                        await pause(TimeSpan.FromMilliseconds(500), deadline.Token).WaitAsync(deadline.Token);
                }
                if (outcome == DnsProbeOutcome.Success)
                {
                    deadline.Token.ThrowIfCancellationRequested();
                    var query = DnsProbePacket.CreateQuery(target);
                    var response = await exchange(query, deadline.Token).WaitAsync(deadline.Token);
                    deadline.Token.ThrowIfCancellationRequested();
                    (outcome, responseCode) = DnsProbePacket.Evaluate(query, response);
                    if (outcome == DnsProbeOutcome.Success)
                    {
                        // Keep the ordinary OS route: binding only the probe could hide broken shared DNS.
                        var candidate = route();
                        deadline.Token.ThrowIfCancellationRequested();
                        observedRoute = candidate;
                        outcome = RouteOutcome(observedRoute);
                    }
                }
            }
            catch (SocketException error)
            { outcome = DnsProbeOutcome.SocketError; socketError = (int)error.SocketErrorCode; }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // An expired route wait is still evidence of the last route failure, not a
                // DNS-server timeout. A caller cancellation is never converted to an observation.
                outcome = observedRoute.Status == DnsRouteStatus.OwnedTunnel
                    ? DnsProbeOutcome.Timeout : RouteOutcome(observedRoute);
            }
            cancellationToken.ThrowIfCancellationRequested();
            attempts[attempt] = new(target, outcome, (int)clock.ElapsedMilliseconds,
                responseCode, socketError, observedRoute);
            observe(new(DateTimeOffset.UtcNow, attempts[0]!, attempts[1], attempts[2]));
            cancellationToken.ThrowIfCancellationRequested();
            if (outcome == DnsProbeOutcome.Success) return;
        }
        cancellationToken.ThrowIfCancellationRequested();
        var last = attempts.LastOrDefault(item => item is not null);
        throw new ClientFault(last?.Outcome is DnsProbeOutcome.RouteMismatch or DnsProbeOutcome.RouteUnavailable
            ? "VPN_DNS_ROUTE_FAILED" : "VPN_DNS_FAILED");
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
