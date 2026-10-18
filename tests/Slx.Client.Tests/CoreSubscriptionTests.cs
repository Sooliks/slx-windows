using System.Text;
using Slx.Client.Core;
using Xunit;

namespace Slx.Client.Tests;

public sealed class CoreSubscriptionTests
{
    [Fact]
    public void ParsesUtf8AndBase64WithIdenticalStableIdsAndMetadata()
    {
        var body = CoreFixtures.Hysteria + "\n" + CoreFixtures.Xhttp;
        var expires = DateTimeOffset.UtcNow.AddDays(30);
        var plain = SubscriptionParser.Parse(body, "SLX · доступ", expires);
        var encoded = SubscriptionParser.Parse(Convert.ToBase64String(Encoding.UTF8.GetBytes(body)));
        Assert.Equal("SLX · доступ", plain.Title);
        Assert.Equal(expires, plain.ExpiresAt);
        Assert.Equal(2, plain.Profiles.Count);
        Assert.Equal(plain.Profiles.Select(profile => profile.Id), encoded.Profiles.Select(profile => profile.Id));
        Assert.Equal("SLX Primary", plain.Profiles[0].Name);
        Assert.Equal("hysteria2", plain.Profiles[0].Kind);
        Assert.Equal("xhttp", plain.Profiles[1].Transport);
        Assert.Equal("/xhttp", plain.Profiles[1].Path);
        Assert.Equal("unsafe", plain.Profiles[1].Fingerprint);
        Assert.All(plain.Profiles, profile =>
        {
            Assert.Equal(64, profile.Id.Length);
            Assert.DoesNotContain(CoreFixtures.Token, profile.Id);
        });
    }

    [Fact]
    public void DeduplicatesUrisWithoutChangingServerSuppliedNames()
    {
        var snapshot = SubscriptionParser.Parse("# provider comment\n" + CoreFixtures.Hysteria + "\n" + CoreFixtures.Hysteria);
        Assert.Single(snapshot.Profiles);
        Assert.Equal("SLX Primary", snapshot.Profiles[0].Name);
    }

    [Fact]
    public void SupportsLegacyRealityWithValidatedPublicKeyAndVision()
    {
        var profile = CoreFixtures.Profile(CoreFixtures.Reality);
        Assert.Equal("tcp", profile.Transport);
        Assert.Equal("xtls-rprx-vision", profile.Flow);
        Assert.Equal("39ac51fb", profile.ShortId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a subscription")]
    [InlineData("https://vpn.example.com/sub/not-a-profile")]
    [InlineData("hysteria2://synthetic@127.0.0.1:8444?sni=vpn.example.com")]
    [InlineData("hysteria2://synthetic@vpn.example.com:8444?insecure=1")]
    [InlineData("hysteria2://synthetic@vpn.example.com:8444?allowInsecure=true")]
    [InlineData("hysteria2://synthetic@vpn.example.com:8444?sni=a.example.com&sni=b.example.com")]
    [InlineData("hysteria2://synthetic@vpn.example.com:8444?x-hwid=another-device")]
    [InlineData("hysteria2://synthetic@vpn.example.com:0")]
    [InlineData("hysteria2://synthetic@vpn.example.com:8444#bad%0Aname")]
    [InlineData("hysteria2://synthetic@vpn.example.com:8444#bad%XXname")]
    public void InvalidOrInsecureProfilesHaveOnlySafeFaultCodes(string body)
    {
        var error = Assert.Throws<ClientFault>(() => SubscriptionParser.Parse(body));
        Assert.Equal("INVALID_SUBSCRIPTION", error.Code);
        Assert.DoesNotContain("synthetic", error.Message);
    }

    [Theory]
    [InlineData("mode=packet-up")]
    [InlineData("security=none")]
    [InlineData("type=ws")]
    [InlineData("alpn=h3")]
    [InlineData("fp=chrome")]
    public void RejectsXhttpParametersThatWouldChangeSupportedTransport(string parameter)
    {
        var pair = parameter.Split('=');
        var current = pair[0] switch
        {
            "mode" => "mode=stream-one", "security" => "security=tls", "type" => "type=xhttp",
            "alpn" => "alpn=h2", _ => "fp=unsafe"
        };
        Assert.Throws<ClientFault>(() => SubscriptionParser.Parse(CoreFixtures.Xhttp.Replace(current, parameter)));
    }

    [Fact]
    public void RejectsOversizeAndInvalidUtf8Base64()
    {
        Assert.Throws<ClientFault>(() => SubscriptionParser.Parse(new string('A', 262145)));
        Assert.Throws<ClientFault>(() => SubscriptionParser.Parse(Convert.ToBase64String([0xff, 0xfe, 0xff])));
    }
}
