using System.Net;
using System.Net.Sockets;
using Slx.Client.Core;

namespace Slx.Client.Service;

/// <summary>Pins the chosen server and, best effort, its other transports to public addresses.</summary>
internal static class TunnelEndpoints
{
    internal static async Task<ConnectPlan> PinAsync(ConnectPlan plan, CancellationToken cancellationToken,
        ConnectPlan? previous = null)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(TimeSpan.FromSeconds(8));
        string host;
        try { host = await ResolveAsync(plan.Profile.Host, budget.Token); }
        catch (SocketException) { throw new ClientFault("ENDPOINT_UNREACHABLE"); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new ClientFault("ENDPOINT_TIMEOUT"); }
        var alternates = await Task.WhenAll((plan.Alternates ?? [])
            .Select(alternate => TryPinAsync(alternate, previous, budget.Token)));
        cancellationToken.ThrowIfCancellationRequested();
        var pinned = alternates.OfType<VpnProfile>().ToList();
        return plan with { Profile = plan.Profile with { Host = host }, Alternates = pinned.Count == 0 ? null : pinned };
    }

    /// <summary>An alternate that cannot be resolved now is skipped; it never blocks the chosen server.</summary>
    private static async Task<VpnProfile?> TryPinAsync(VpnProfile alternate, ConnectPlan? previous, CancellationToken token)
    {
        var known = previous?.Alternates?.FirstOrDefault(item => item.Id == alternate.Id);
        if (known is not null && IPAddress.TryParse(known.Host, out _)) return alternate with { Host = known.Host };
        try { return alternate with { Host = await ResolveAsync(alternate.Host, token) }; }
        catch (Exception error) when (error is SocketException or OperationCanceledException or ClientFault or ArgumentException)
        { return null; }
    }

    private static async Task<string> ResolveAsync(string host, CancellationToken token)
    {
        var addresses = await Dns.GetHostAddressesAsync(host, token);
        if (addresses.Length == 0 || addresses.Any(address => !EndpointPolicy.IsPublicAddress(address)))
            throw new ClientFault("ENDPOINT_NOT_PUBLIC");
        var selected = addresses.FirstOrDefault(address => address.AddressFamily == AddressFamily.InterNetwork) ?? addresses[0];
        return selected.ToString();
    }

    /// <summary>True when the pinned plan already holds every alternate the UI offers (by profile id).</summary>
    internal static bool HasAlternates(ConnectPlan pinned, ConnectPlan offered)
    {
        var known = (pinned.Alternates ?? []).Select(alternate => alternate.Id).ToHashSet(StringComparer.Ordinal);
        return (offered.Alternates ?? []).All(alternate => known.Contains(alternate.Id));
    }

    internal static IEnumerable<string> Hosts(ConnectPlan plan) =>
        new[] { plan.Profile.Host }.Concat((plan.Alternates ?? []).Select(alternate => alternate.Host));
}
