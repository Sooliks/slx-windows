using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Slx.Client.Core;
using Windows.Win32;
using Windows.Win32.NetworkManagement.IpHelper;
using Windows.Win32.Networking.WinSock;

namespace Slx.Client.Windows;

public sealed record OutboundBinding(Guid Identity, string Name)
{
    public override string ToString() => "OutboundBinding [redacted]";
}

/// <summary>Read the best usable route outside our TUN, including while its split defaults exist.</summary>
public static class PhysicalEgress
{
    public static unsafe OutboundBinding? Read(string address)
    {
        if (!IPAddress.TryParse(address, out var destination) || !EndpointPolicy.IsPublicAddress(destination))
            throw new ClientFault("ENDPOINT_NOT_PUBLIC");
        if (destination.IsIPv4MappedToIPv6) destination = destination.MapToIPv4();
        var target = OutboundInterface.CreateAddress(destination);
        var candidates = new List<EgressCandidate>();
        try
        {
            foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (!Guid.TryParse(adapter.Id, out var identity) || identity == OutboundInterface.TunnelIdentity
                    || adapter.OperationalStatus != OperationalStatus.Up || adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                uint index;
                try
                {
                    index = (uint)(destination.AddressFamily == AddressFamily.InterNetwork
                        ? adapter.GetIPProperties().GetIPv4Properties().Index : adapter.GetIPProperties().GetIPv6Properties().Index);
                }
                catch (NetworkInformationException) { continue; }
                if (OutboundInterface.ReadRoute(index, in target, out var route) != 0) continue;
                var row = new MIB_IPINTERFACE_ROW { Family = (ADDRESS_FAMILY)target.Family, InterfaceIndex = index };
                if (PInvoke.GetIpInterfaceEntry(&row) != 0 || row.Connected == 0) continue;
                candidates.Add(new(identity, adapter.Name, route.PrefixLength, (ulong)route.Metric + row.Metric));
            }
            return Choose(candidates);
        }
        catch (NetworkInformationException) { return null; }
    }

    internal static OutboundBinding? Choose(IEnumerable<EgressCandidate> candidates)
    {
        var chosen = candidates.Where(x => x.Identity != Guid.Empty && x.Identity != OutboundInterface.TunnelIdentity
                && !string.IsNullOrWhiteSpace(x.Name) && x.Name.Length <= 256 && !x.Name.Any(char.IsControl)
                && x.Name != "auto" && !x.Name.Equals("SLX VPN", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.Prefix).ThenBy(x => x.Metric).ThenBy(x => x.Identity).FirstOrDefault();
        return chosen is null ? null : new(chosen.Identity, chosen.Name);
    }
    internal sealed record EgressCandidate(Guid Identity, string Name, byte Prefix, ulong Metric);
}
