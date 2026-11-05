using System.Text.Json;
using Slx.Client.Core;
using Slx.Client.Service;
using Xunit;

namespace Slx.Client.Tests;

public sealed class TransportFallbackTests
{
    private static VpnProfile Named(string uri, string name, string host) =>
        CoreFixtures.Profile(uri) with { Name = name, Host = host };

    [Fact]
    public void AlternatesAreOtherTransportsOfTheSameServerOrLocationOnly()
    {
        var primary = Named(CoreFixtures.Hysteria, "SLX · Нидерланды · Hysteria2", "nl1.example.com");
        var sameHost = Named(CoreFixtures.Reality, "Something else", "NL1.example.com");
        var sameName = Named(CoreFixtures.Xhttp, "SLX · Нидерланды · VLESS", "nl2.example.com");
        var otherCountry = Named(CoreFixtures.Reality, "SLX · Германия · VLESS", "de.example.com");
        var duplicateTransport = Named(CoreFixtures.Reality, "SLX · Нидерланды REALITY", "nl3.example.com");
        var alternates = TransportAlternates.For(primary, [primary, otherCountry, sameHost, sameName, duplicateTransport, null]);
        Assert.NotNull(alternates);
        Assert.Equal([sameHost.Id, sameName.Id], alternates.Select(profile => profile.Id).ToArray());
        Assert.DoesNotContain(alternates, profile => profile.Host == "de.example.com");
        Assert.Null(TransportAlternates.For(otherCountry, [primary, sameName]));
    }

    [Fact]
    public void TransportWordsAndDefaultNamesNeverMatchDifferentServers()
    {
        Assert.Equal("нидерланды", TransportAlternates.LocationName("🇳🇱 SLX · Нидерланды (Hysteria2)"));
        Assert.Equal("", TransportAlternates.LocationName("SLX · VLESS"));
        var first = Named(CoreFixtures.Hysteria, "SLX · Hysteria2", "a.example.com");
        var second = Named(CoreFixtures.Reality, "SLX · VLESS", "b.example.com");
        Assert.False(TransportAlternates.SameLocation(first, second));
        Assert.Equal("hysteria2", TransportAlternates.Label(first));
        Assert.Equal("vless-reality", TransportAlternates.Label(second));
        Assert.Equal("vless-xhttp", TransportAlternates.Label(CoreFixtures.Profile(CoreFixtures.Xhttp)));
    }

    [Fact]
    public void PlanValidationBoundsAlternatesAndRejectsDuplicates()
    {
        var plan = CoreFixtures.Plan();
        var reality = CoreFixtures.Profile(CoreFixtures.Reality);
        PlanValidator.Validate(plan with { Alternates = [reality] });
        Assert.Throws<ClientFault>(() => PlanValidator.Validate(plan with { Alternates = [reality, reality] }));
        Assert.Throws<ClientFault>(() => PlanValidator.Validate(plan with { Alternates = [plan.Profile] }));
        Assert.Throws<ClientFault>(() => PlanValidator.Validate(plan with { Alternates = [reality with { Port = 0 }] }));
        var many = Enumerable.Range(0, TransportAlternates.Limit + 1).Select(index => reality with { Id = "id-" + index }).ToList();
        Assert.Throws<ClientFault>(() => PlanValidator.Validate(plan with { Alternates = many }));
    }

    [Fact]
    public void PlanWithoutAlternatesKeepsItsPreviousWireShape()
    {
        var plan = CoreFixtures.Plan();
        Assert.DoesNotContain("Alternates", JsonSerializer.Serialize(plan));
        var json = JsonSerializer.Serialize(plan with { Alternates = [CoreFixtures.Profile(CoreFixtures.Reality)] });
        var restored = JsonSerializer.Deserialize<ConnectPlan>(json)!;
        Assert.Single(restored.Alternates!);
    }

    [Fact]
    public void TwoFailedCyclesSwitchTransportRoundRobinAndSuccessResetsTheCount()
    {
        var rotation = new TransportRotation();
        var reality = CoreFixtures.Profile(CoreFixtures.Reality);
        var xhttp = CoreFixtures.Profile(CoreFixtures.Xhttp);
        rotation.Reset(CoreFixtures.Plan() with { Alternates = [reality, xhttp] });
        Assert.Equal("hysteria2", TransportAlternates.Label(rotation.Current));
        Assert.False(rotation.Failed("HEALTH_PROBE_FAILED"));
        Assert.Equal(0, rotation.Index);
        Assert.True(rotation.Failed("VPN_PROBE_FAILED"));
        Assert.Same(reality, rotation.Current);
        var observed = rotation.Observe(DateTimeOffset.UnixEpoch)!;
        Assert.Equal(("vless-reality", 2, 3, 3, "VPN_PROBE_FAILED"),
            (observed.Transport, observed.TransportIndex, observed.TransportCount, observed.Attempt, observed.Reason));
        rotation.Succeeded();
        Assert.False(rotation.Failed("HEALTH_PROBE_FAILED"));
        Assert.Same(reality, rotation.Current);
        Assert.True(rotation.Failed("HEALTH_PROBE_FAILED"));
        Assert.Same(xhttp, rotation.Current);
        Assert.True(rotation.Advance("VPN_PROBE_FAILED"));
        Assert.Equal(0, rotation.Index);
    }

    [Fact]
    public void SingleTransportNeverSwitchesAndRepinningKeepsTheWorkingTransport()
    {
        var rotation = new TransportRotation();
        rotation.Reset(CoreFixtures.Plan());
        for (int index = 0; index < 5; index++) Assert.False(rotation.Failed("VPN_PROBE_FAILED"));
        Assert.False(rotation.Advance("VPN_PROBE_FAILED"));
        Assert.Equal(0, rotation.Index);
        var reality = CoreFixtures.Profile(CoreFixtures.Reality);
        var plan = CoreFixtures.Plan() with { Alternates = [reality] };
        rotation.Reset(plan);
        rotation.Advance("VPN_PROBE_FAILED");
        rotation.Ensure(plan with { Alternates = [reality with { Host = "93.184.216.36" }] });
        Assert.Equal(1, rotation.Index);
        rotation.Ensure(plan with { Profile = reality, Alternates = null });
        Assert.Equal(0, rotation.Index);
        rotation.Clear();
        Assert.Null(rotation.Observe(DateTimeOffset.UnixEpoch));
        Assert.Throws<ClientFault>(() => rotation.Current);
    }

    [Fact]
    public void RunningConnectionAdoptsAlternatesOnlyWhenTheUiOffersNewOnes()
    {
        var reality = CoreFixtures.Profile(CoreFixtures.Reality);
        var upgraded = CoreFixtures.Plan();
        var offered = CoreFixtures.Plan() with { Alternates = [reality] };
        Assert.False(TunnelEndpoints.HasAlternates(upgraded, offered));
        Assert.True(TunnelEndpoints.HasAlternates(offered with { Alternates = [reality with { Host = "93.184.216.34" }] }, offered));
        Assert.True(TunnelEndpoints.HasAlternates(upgraded, upgraded));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 3)]
    [InlineData(4, 5)]
    [InlineData(5, 8)]
    [InlineData(6, 10)]
    [InlineData(30, 10)]
    public void ReconnectPausesStayShortWhileTheTunProcessKeepsDirectTraffic(int failures, int seconds) =>
        Assert.Equal(TimeSpan.FromSeconds(seconds), RecoveryBackoff.Reconnect(failures));
}
