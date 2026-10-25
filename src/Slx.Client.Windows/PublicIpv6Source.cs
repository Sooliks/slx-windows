using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Slx.Client.Core;

namespace Slx.Client.Windows;

/// <summary>Eligibility for an ordinary global IPv6 route read; does not change source-address policy.</summary>
public static class PublicIpv6Source
{
    public static bool IsAvailable()
    {
        try
        {
            return IsAvailable(NetworkInterface.GetAllNetworkInterfaces()
                .Where(adapter => adapter.OperationalStatus == OperationalStatus.Up).SelectMany(adapter =>
                adapter.GetIPProperties().UnicastAddresses.Select(address => new Candidate(address.Address,
                    adapter.OperationalStatus == OperationalStatus.Up, address.DuplicateAddressDetectionState))));
        }
        catch (NetworkInformationException) { throw new ClientFault("VPN_TRAFFIC_ROUTE_FAILED"); }
    }

    internal static bool IsAvailable(IEnumerable<Candidate> candidates) => candidates.Any(candidate =>
        candidate.Up && candidate.State == DuplicateAddressDetectionState.Preferred
        && candidate.Address.AddressFamily == AddressFamily.InterNetworkV6
        && !candidate.Address.IsIPv4MappedToIPv6
        && EndpointPolicy.IsPublicAddress(candidate.Address));

    internal sealed record Candidate(IPAddress Address, bool Up, DuplicateAddressDetectionState State);
}
