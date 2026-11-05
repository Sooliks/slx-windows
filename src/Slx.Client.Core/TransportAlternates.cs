namespace Slx.Client.Core;

/// <summary>
/// Other transports of the same server or location from the user's subscription. A different country is
/// never substituted: only the same endpoint host or the same location name without transport words matches.
/// </summary>
public static class TransportAlternates
{
    public const int Limit = 4;
    private static readonly HashSet<string> TransportWords = new(StringComparer.Ordinal)
    { "slx", "hysteria", "hysteria2", "hy2", "vless", "reality", "xhttp", "tcp", "raw", "udp", "quic", "h2", "h3", "tls" };

    public static List<VpnProfile>? For(VpnProfile primary, IEnumerable<VpnProfile?> profiles)
    {
        ArgumentNullException.ThrowIfNull(primary);
        ArgumentNullException.ThrowIfNull(profiles);
        var seen = new HashSet<string>(StringComparer.Ordinal) { Label(primary) };
        var result = new List<VpnProfile>();
        foreach (var profile in profiles)
        {
            if (profile is null || profile.Id == primary.Id || !SameLocation(primary, profile) || !Valid(profile)) continue;
            if (!seen.Add(Label(profile))) continue;
            result.Add(profile);
            if (result.Count == Limit) break;
        }
        return result.Count == 0 ? null : result;
    }

    private static bool Valid(VpnProfile profile)
    {
        try { PlanValidator.ValidateProfile(profile); return true; }
        catch (ClientFault) { return false; }
    }

    public static string Label(VpnProfile profile) => profile.Kind == "hysteria2" ? "hysteria2"
        : profile.Transport == "xhttp" ? "vless-xhttp" : "vless-reality";

    public static bool SameLocation(VpnProfile first, VpnProfile second)
    {
        if (string.Equals(first.Host, second.Host, StringComparison.OrdinalIgnoreCase)) return true;
        string name = LocationName(first.Name);
        return name.Length > 0 && name == LocationName(second.Name);
    }

    public static string LocationName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "";
        var words = new List<string>();
        var current = new System.Text.StringBuilder();
        foreach (char character in name.ToLowerInvariant() + " ")
        {
            if (char.IsLetterOrDigit(character)) { current.Append(character); continue; }
            if (current.Length == 0) continue;
            string word = current.ToString();
            current.Clear();
            if (!TransportWords.Contains(word)) words.Add(word);
        }
        return string.Join(' ', words);
    }
}
