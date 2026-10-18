using Slx.Client.Core;

namespace Slx.Client.Tests;

internal static class CoreFixtures
{
    public const string Token = "04c7bc1d-636a-4ea4-b786-59f3977d88c4";
    public const string Hysteria = "hysteria2://" + Token + "@vpn.example.com:8444/?sni=vpn.example.com#SLX%20Primary";
    public const string Xhttp = "vless://" + Token + "@vpn.example.com:8443?encryption=none&security=tls&type=xhttp&path=%2Fxhttp&mode=stream-one&sni=vpn.example.com&fp=unsafe&alpn=h2#SLX%20Fallback";
    public const string Reality = "vless://" + Token + "@vpn.example.com:443?security=reality&type=tcp&sni=www.example.com&pbk=AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA&sid=39ac51fb&fp=chrome&flow=xtls-rprx-vision#Legacy";
    public static VpnProfile Profile(string? uri = null) => SubscriptionParser.Parse(uri ?? Hysteria).Profiles[0];
    public static AppRule App(string path = @"C:\Apps\Browser\browser.exe", bool enabled = true) =>
        new(Guid.NewGuid(), "Browser", path, enabled);
    public static ConnectPlan Plan(RoutingMode mode = RoutingMode.SelectedApplications) =>
        new(Profile(), mode, [App()]);
}
