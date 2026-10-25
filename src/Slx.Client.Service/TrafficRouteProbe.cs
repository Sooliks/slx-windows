using System.Net;
using Slx.Client.Core;
using Slx.Client.Windows;

namespace Slx.Client.Service;

/// <summary>Ordinary data routes are separate from proxy connectivity and the protected DNS host routes.</summary>
internal static class TrafficRouteProbe
{
    // Route lookups only: no DNS query or packet is sent to these representative addresses.
    private static readonly IPAddress Low = IPAddress.Parse("8.8.8.8");
    private static readonly IPAddress High = IPAddress.Parse("142.250.0.1");
    private static readonly IPAddress V6 = IPAddress.Parse("2001:4860:4860::8888");
    internal const string FailureCode = "VPN_TRAFFIC_ROUTE_FAILED";

    internal static Task CheckAsync(string ownedAdapterId, Action<TrafficRouteCheckObservation> observe,
        CancellationToken cancellationToken) => CheckAsync(token => Observe(ownedAdapterId, token), observe,
            cancellationToken);

    internal static async Task CheckAsync(Func<CancellationToken, TrafficRouteCheckObservation> read,
        Action<TrafficRouteCheckObservation> observe, CancellationToken cancellationToken,
        Func<TimeSpan, CancellationToken, Task>? pause = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        pause ??= Task.Delay;
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(TimeSpan.FromSeconds(3));
        try
        {
            for (int sample = 0; sample < 6; sample++)
            {
                budget.Token.ThrowIfCancellationRequested();
                var result = read(budget.Token);
                budget.Token.ThrowIfCancellationRequested();
                observe(result);
                budget.Token.ThrowIfCancellationRequested();
                if (IsReady(result)) return;
                if (sample < 5)
                    await pause(TimeSpan.FromMilliseconds(500), budget.Token).WaitAsync(budget.Token);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { }
        cancellationToken.ThrowIfCancellationRequested();
        throw new ClientFault(FailureCode);
    }

    internal static TrafficRouteCheckObservation Observe(string ownedAdapterId, CancellationToken token)
        => Observe(address => TunnelDataRoute.Observe(ownedAdapterId, address), PublicIpv6Source.IsAvailable, token);

    internal static TrafficRouteCheckObservation Observe(Func<IPAddress, DnsRouteObservation> read,
        Func<bool> ipv6Available, CancellationToken token)
    {
        DnsRouteObservation Read(IPAddress address)
        {
            token.ThrowIfCancellationRequested();
            var result = read(address);
            token.ThrowIfCancellationRequested();
            return result;
        }
        var low = Read(Low);
        var high = Read(High);
        token.ThrowIfCancellationRequested();
        bool hasIpv6 = ipv6Available();
        token.ThrowIfCancellationRequested();
        var ipv6 = hasIpv6 ? Read(V6) : null;
        return new(DateTimeOffset.UtcNow, low, high, ipv6);
    }

    internal static bool IsReady(TrafficRouteCheckObservation observation) =>
        observation.Ipv4Low.Status == DnsRouteStatus.OwnedTunnel
        && observation.Ipv4High.Status == DnsRouteStatus.OwnedTunnel
        && (observation.Ipv6 is null || observation.Ipv6.Status == DnsRouteStatus.OwnedTunnel);

    internal static void RequireReady(TrafficRouteCheckObservation observation)
    {
        if (!IsReady(observation)) throw new ClientFault(FailureCode);
    }
}
