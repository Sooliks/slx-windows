namespace Slx.Client.Core;

internal static class ProfileUriParser
{
    public static VpnProfile Parse(string line)
    {
        _ = Decode(line);
        if (!Uri.TryCreate(line, UriKind.Absolute, out var uri) || uri.Port is < 1 or > 65535
            || uri.AbsolutePath is not ("" or "/") || string.IsNullOrWhiteSpace(uri.UserInfo))
            throw new ClientFault("INVALID_SUBSCRIPTION");
        var query = Query(uri.Query);
        var name = Decode(uri.Fragment.TrimStart('#'));
        if (name.Length == 0) name = uri.Scheme is "hy2" or "hysteria2" ? "SLX · Hysteria2" : "SLX · VLESS";
        var secret = Decode(uri.UserInfo);
        if (secret.Contains(':')) throw new ClientFault("INVALID_SUBSCRIPTION");
        var sni = Get(query, "sni", uri.IdnHost);
        var profile = uri.Scheme switch
        {
            "hy2" or "hysteria2" => Hysteria(uri, line, query, name, secret, sni),
            "vless" => Vless(uri, line, query, name, secret, sni),
            _ => throw new ClientFault("INVALID_SUBSCRIPTION")
        };
        PlanValidator.ValidateProfile(profile);
        return profile;
    }

    private static VpnProfile Hysteria(Uri uri, string line, Dictionary<string, string> query,
        string name, string secret, string sni)
    {
        Keys(query, ["sni", "alpn", "insecure", "allowInsecure"]);
        if (Get(query, "insecure", "0") is not ("0" or "false")
            || Get(query, "allowInsecure", "0") is not ("0" or "false"))
            throw new ClientFault("INVALID_SUBSCRIPTION");
        return new VpnProfile(SubscriptionParser.Id(line), name, "hysteria2", uri.IdnHost, uri.Port,
            secret, sni, Transport: "hysteria", Alpn: Get(query, "alpn", "h3"));
    }

    private static VpnProfile Vless(Uri uri, string line, Dictionary<string, string> query,
        string name, string secret, string sni)
    {
        Keys(query, ["encryption", "security", "type", "path", "mode", "sni", "fp", "alpn", "pbk", "sid", "flow", "spx"]);
        if (Get(query, "encryption", "none") != "none") throw new ClientFault("INVALID_SUBSCRIPTION");
        var transport = Get(query, "type", "tcp");
        var security = Get(query, "security", "");
        if (transport == "xhttp")
        {
            if (security != "tls" || Get(query, "mode", "stream-one") != "stream-one"
                || query.ContainsKey("pbk") || query.ContainsKey("sid") || query.ContainsKey("spx"))
                throw new ClientFault("INVALID_SUBSCRIPTION");
            return new VpnProfile(SubscriptionParser.Id(line), name, "vless", uri.IdnHost, uri.Port, secret, sni,
                Transport: "xhttp", Path: Get(query, "path", "/xhttp"), Flow: Get(query, "flow", ""),
                Fingerprint: Get(query, "fp", "unsafe"), Alpn: Get(query, "alpn", "h2"));
        }
        if (security != "reality" || transport is not ("tcp" or "raw") || query.ContainsKey("path")
            || query.ContainsKey("mode") || (query.TryGetValue("spx", out var spider) && spider is not ("" or "/")))
            throw new ClientFault("INVALID_SUBSCRIPTION");
        return new VpnProfile(SubscriptionParser.Id(line), name, "vless", uri.IdnHost, uri.Port, secret, sni,
            Get(query, "pbk", ""), Get(query, "sid", ""), transport, Flow: Get(query, "flow", ""),
            Fingerprint: Get(query, "fp", "chrome"), Alpn: query.GetValueOrDefault("alpn"));
    }

    private static Dictionary<string, string> Query(string source)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in source.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = entry.Split('=', 2);
            var key = Decode(pair[0]); var value = pair.Length == 2 ? Decode(pair[1]) : "";
            if (key.Length == 0 || key.Length > 64 || value.Length > 2048 || !result.TryAdd(key, value))
                throw new ClientFault("INVALID_SUBSCRIPTION");
        }
        return result;
    }

    private static string Decode(string text)
    {
        for (var index = 0; index < text.Length; index++)
            if (text[index] == '%' && (index + 2 >= text.Length || !Uri.IsHexDigit(text[index + 1]) || !Uri.IsHexDigit(text[index + 2])))
                throw new ClientFault("INVALID_SUBSCRIPTION");
        var decoded = Uri.UnescapeDataString(text);
        if (decoded.Any(char.IsControl)) throw new ClientFault("INVALID_SUBSCRIPTION");
        return decoded;
    }
    private static string Get(Dictionary<string, string> query, string key, string fallback) => query.GetValueOrDefault(key, fallback);
    private static void Keys(Dictionary<string, string> query, string[] allowed)
    {
        if (query.Keys.Any(key => !allowed.Contains(key, StringComparer.Ordinal)))
            throw new ClientFault("INVALID_SUBSCRIPTION");
    }
}
