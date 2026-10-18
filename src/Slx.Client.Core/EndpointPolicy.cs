using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace Slx.Client.Core;

/// <summary>Literal checks only; the service must also validate every DNS answer before starting Xray.</summary>
public static partial class EndpointPolicy
{
    public static void ValidateHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host) || host.Length > 253 || host != host.Trim())
            throw new ClientFault("INVALID_PROFILE");
        if (IPAddress.TryParse(host, out var address))
        {
            if (!IsPublicAddress(address)) throw new ClientFault("INVALID_PROFILE");
            return;
        }
        if (!HostPattern().IsMatch(host) || !host.Contains('.') || host.EndsWith(".local", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase))
            throw new ClientFault("INVALID_PROFILE");
    }

    public static bool IsPublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) return IsPublicAddress(address.MapToIPv4());
        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var a = bytes[0]; var b = bytes[1];
            return a is not (0 or 10 or 127) && a < 224
                && !(a == 100 && b is >= 64 and <= 127)
                && !(a == 169 && b == 254) && !(a == 172 && b is >= 16 and <= 31)
                && !(a == 192 && (b == 168 || b == 0 || (b == 88 && bytes[2] == 99)))
                && !(a == 198 && (b is 18 or 19 || (b == 51 && bytes[2] == 100)))
                && !(a == 203 && b == 0 && bytes[2] == 113);
        }
        // Globally routed IPv6 unicast only; block transition/documentation/reserved ranges.
        return address.AddressFamily == AddressFamily.InterNetworkV6 && address.ScopeId == 0 && bytes[0] is >= 0x20 and <= 0x3f
            && !(bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] == 0x0d && bytes[3] == 0xb8)
            && !(bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] < 2)
            && !(bytes[0] == 0x20 && bytes[1] == 0x02)
            && !(bytes[0] == 0x3f && bytes[1] == 0xff && (bytes[2] & 0xf0) == 0);
    }

    [GeneratedRegex(@"\A[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?(?:\.[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?)+\z",
        RegexOptions.CultureInvariant)]
    private static partial Regex HostPattern();
}
