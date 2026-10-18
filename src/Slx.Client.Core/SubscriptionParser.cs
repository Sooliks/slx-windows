using System.Security.Cryptography;
using System.Text;

namespace Slx.Client.Core;

public static class SubscriptionParser
{
    private const int MaxBodyLength = 262144;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static SubscriptionSnapshot Parse(string body, string? title = null, DateTimeOffset? expiresAt = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(body) || body.Length > MaxBodyLength)
                throw new ClientFault("INVALID_SUBSCRIPTION");
            var text = body.Trim().TrimStart('\uFEFF');
            if (!text.Contains("://", StringComparison.Ordinal))
                text = StrictUtf8.GetString(Convert.FromBase64String(text)).Trim().TrimStart('\uFEFF');
            var profiles = new List<VpnProfile>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var line in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (line.StartsWith('#')) continue;
                if (line.Length > 8192 || profiles.Count >= 64) throw new ClientFault("INVALID_SUBSCRIPTION");
                var profile = ProfileUriParser.Parse(line);
                if (ids.Add(profile.Id)) profiles.Add(profile);
            }
            if (profiles.Count == 0) throw new ClientFault("INVALID_SUBSCRIPTION");
            var safeTitle = string.IsNullOrWhiteSpace(title) ? "SLX VPN" : title.Trim();
            if (safeTitle.Length > 160 || safeTitle.Any(char.IsControl)) throw new ClientFault("INVALID_SUBSCRIPTION");
            return new SubscriptionSnapshot(safeTitle, expiresAt, profiles, DateTimeOffset.UtcNow);
        }
        catch (ClientFault) { throw new ClientFault("INVALID_SUBSCRIPTION"); }
        catch (Exception error) when (error is FormatException or DecoderFallbackException or UriFormatException or ArgumentException)
        {
            throw new ClientFault("INVALID_SUBSCRIPTION");
        }
    }

    internal static string Id(string source) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(source)));
}
