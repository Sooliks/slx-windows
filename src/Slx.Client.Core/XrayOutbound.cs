namespace Slx.Client.Core;

internal static class XrayOutbound
{
    public static object Build(VpnProfile profile)
    {
        if (profile.Kind == "hysteria2")
            return new
            {
                tag = "vpn", protocol = "hysteria",
                settings = new { version = 2, address = profile.Host, port = profile.Port },
                streamSettings = new
                {
                    network = "hysteria", security = "tls",
                    hysteriaSettings = new { version = 2, auth = profile.Secret },
                    tlsSettings = new { serverName = profile.ServerName, alpn = new[] { "h3" }, allowInsecure = false }
                }
            };
        object transport = profile.Transport == "xhttp"
            ? new
            {
                network = "xhttp", security = "tls",
                xhttpSettings = new { path = profile.Path, mode = "stream-one" },
                // fp=unsafe in the subscription selects native TLS, never insecure certificate validation.
                tlsSettings = new { serverName = profile.ServerName, alpn = new[] { "h2" }, allowInsecure = false, fingerprint = "unsafe" }
            }
            : new
            {
                network = "raw", security = "reality",
                realitySettings = new
                {
                    serverName = profile.ServerName, fingerprint = profile.Fingerprint is null or "" ? "chrome" : profile.Fingerprint,
                    publicKey = profile.PublicKey, shortId = profile.ShortId ?? "", spiderX = "/"
                }
            };
        return new
        {
            tag = "vpn", protocol = "vless",
            settings = new
            {
                vnext = new[]
                {
                    new
                    {
                        address = profile.Host, port = profile.Port,
                        users = new[] { new { id = profile.Secret, encryption = "none", flow = profile.Flow ?? "" } }
                    }
                }
            },
            streamSettings = transport
        };
    }
}
