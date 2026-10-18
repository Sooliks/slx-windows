using System.Text;
using Slx.Client.Core;
using Xunit;

namespace Slx.Client.Tests;

public sealed class CoreEmitterRegressionTests
{
    // Mirrors the public website connectionProfile emitter shape using synthetic credentials only.
    private static string Hysteria => "hysteria2://" + CoreFixtures.Token
        + "@vpn.example.com:8444?sni=vpn.example.com#"
        + Uri.EscapeDataString("SLX · Москва → Амстердам · Hysteria2");
    private static string Xhttp => "vless://" + CoreFixtures.Token
        + "@vpn.example.com:8443?encryption=none&security=tls&type=xhttp&path=%2Fxhttp&mode=stream-one"
        + "&sni=vpn.example.com&fp=unsafe&alpn=h2#" + Uri.EscapeDataString("SLX · Москва → Амстердам · VLESS");

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void ProductionEmitterShapePreservesBothProtocolsWithTheSameDeviceCredential(bool base64, bool crlf)
    {
        var separator = crlf ? "\r\n" : "\n";
        var body = Hysteria + separator + Xhttp + separator;
        if (base64) body = Convert.ToBase64String(Encoding.UTF8.GetBytes(body));
        var snapshot = SubscriptionParser.Parse(body, "SLX VPN");
        Assert.Equal(["hysteria2", "vless"], snapshot.Profiles.Select(profile => profile.Kind).ToArray());
        Assert.Equal(2, snapshot.Profiles.Select(profile => profile.Id).Distinct().Count());
        Assert.All(snapshot.Profiles, profile => Assert.Equal(CoreFixtures.Token, profile.Secret));
        Assert.Equal("SLX · Москва → Амстердам · Hysteria2", snapshot.Profiles[0].Name);
        Assert.Equal("h3", snapshot.Profiles[0].Alpn);
        Assert.Equal("xhttp", snapshot.Profiles[1].Transport);
        Assert.Equal("h2", snapshot.Profiles[1].Alpn);
        Assert.Equal("unsafe", snapshot.Profiles[1].Fingerprint);
    }

    [Fact]
    public void InsecureHysteriaNeverSilentlyFallsBackToOnlyVless()
    {
        var unsafeBody = Hysteria.Replace("?sni=", "?insecure=1&sni=") + "\n" + Xhttp;
        Assert.Equal("INVALID_SUBSCRIPTION",
            Assert.Throws<ClientFault>(() => SubscriptionParser.Parse(unsafeBody)).Code);
    }
}
