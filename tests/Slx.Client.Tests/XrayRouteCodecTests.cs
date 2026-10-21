using System.Text.Json;
using Slx.Client.Core;
using Slx.Client.Windows;
using Xunit;
namespace Slx.Client.Tests;
public sealed class XrayRouteCodecTests
{
    [Fact]
    public void ApiHasNoListenerAndItsGuardRequiresAuthenticatedProbeAndExactDomain()
    {
        var plan = CoreFixtures.Plan();
        using var config = JsonDocument.Parse(XrayConfigBuilder.Build(plan, 18899, "probeUser00000000", "probePassword0000"));
        var api = config.RootElement.GetProperty("api");
        Assert.False(api.TryGetProperty("listen", out _));
        Assert.Equal(new[] { "RoutingService" }, api.GetProperty("services").EnumerateArray().Select(value => Assert.IsType<string>(value.GetString())).ToArray());
        var rules = config.RootElement.GetProperty("routing").GetProperty("rules"); var guard = rules[0];
        Assert.Equal(XrayRouteProtocol.ApiTag, guard.GetProperty("outboundTag").GetString());
        Assert.Equal(new[] { "probe" }, guard.GetProperty("inboundTag").EnumerateArray().Select(value => Assert.IsType<string>(value.GetString())).ToArray());
        Assert.Equal(new[] { "full:" + XrayRouteProtocol.Host }, guard.GetProperty("domain").EnumerateArray().Select(value => Assert.IsType<string>(value.GetString())).ToArray());
        Assert.Equal("80", guard.GetProperty("port").GetString()); Assert.Equal("tcp", guard.GetProperty("network").GetString());
        Assert.Equal(XrayRouteProtocol.MarkerPrefix + XrayRouteRevision.For(plan), rules[rules.GetArrayLength() - 1].GetProperty("ruleTag").GetString());
    }
    [Fact]
    public void RevisionIsAStableEnabledPathSetWithProfileAndModeBinding()
    {
        var original = CoreFixtures.Plan(); var app = original.Applications[0];
        var same = original with { Applications = [CoreFixtures.App(@"C:\Disabled\off.exe", false),
            app with { Id = Guid.NewGuid(), Name = "Renamed", ExecutablePath = app.ExecutablePath.ToUpperInvariant().Replace('\\', '/') }] };
        Assert.Equal(XrayRouteRevision.For(original), XrayRouteRevision.For(same));
        Assert.NotEqual(XrayRouteRevision.For(original), XrayRouteRevision.For(original with { Mode = RoutingMode.WholeComputer }));
        Assert.NotEqual(XrayRouteRevision.For(original), XrayRouteRevision.For(original with { Applications = [CoreFixtures.App(@"C:\Other\app.exe")] }));
        Assert.NotEqual(XrayRouteRevision.For(original), XrayRouteRevision.For(original with { Profile = original.Profile with { Port = 9443 } }));
        var whole = original with { Mode = RoutingMode.WholeComputer };
        Assert.Equal(XrayRouteRevision.For(whole), XrayRouteRevision.For(whole with { Applications = [] }));
    }
    [Theory]
    [InlineData(RoutingMode.SelectedApplications, "vpn.example.com")]
    [InlineData(RoutingMode.WholeComputer, "1.1.1.1")]
    [InlineData(RoutingMode.WholeComputer, "2606:4700:4700::1111")]
    public void JsonAndPinnedProtobufUseTheSameCompleteOrderedRuleSet(RoutingMode mode, string host)
    {
        var plan = CoreFixtures.Plan(mode); plan = plan with { Profile = plan.Profile with { Host = host } };
        using var config = JsonDocument.Parse(XrayConfigBuilder.Build(plan, 18899, "probeUser00000000", "probePassword0000"));
        var expected = config.RootElement.GetProperty("routing").GetProperty("rules").EnumerateArray().ToArray();
        var request = Parse(XrayRouteProtocol.BuildReplacement(plan));
        Assert.Equal(0ul, request.Single(field => field.Id == 2).Number);
        var typed = Parse(request.Single(field => field.Id == 1).Bytes!);
        Assert.Equal("xray.app.router.Config", System.Text.Encoding.UTF8.GetString(typed.Single(field => field.Id == 1).Bytes!));
        var rules = Parse(typed.Single(field => field.Id == 2).Bytes!).Where(field => field.Id == 2).ToArray();
        Assert.Equal(expected.Length, rules.Length);
        for (int index = 0; index < rules.Length; index++)
        {
            var actual = Parse(rules[index].Bytes!); var json = expected[index];
            Assert.Equal(json.GetProperty("outboundTag").GetString(), Text(actual, 1).Single());
            foreach (var property in new[] { ("inboundTag", 8), ("process", 21), ("ruleTag", 19) })
            {
                var strings = json.TryGetProperty(property.Item1, out var value)
                    ? value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().Select(item => item.GetString()).ToArray() : [value.GetString()] : [];
                Assert.Equal(strings, Text(actual, property.Item2));
            }
            if (json.TryGetProperty("port", out var port))
            {
                var range = Parse(Parse(actual.Single(field => field.Id == 14).Bytes!).Single().Bytes!);
                Assert.Equal(ulong.Parse(port.GetString()!), range.Single(field => field.Id == 1).Number);
                Assert.Equal(range[0].Number, range[1].Number);
            }
            if (json.TryGetProperty("ip", out var ips)) Assert.Equal(ips.GetArrayLength(), actual.Count(field => field.Id == 10));
            if (json.TryGetProperty("domain", out var domains))
            {
                var domain = Parse(Parse(actual.Single(field => field.Id == 2).Bytes!).Single(field => field.Id == 2).Bytes!);
                Assert.Equal(3ul, domain.Single(field => field.Id == 1).Number);
                Assert.Equal(domains[0].GetString()![5..], Text(domain, 2).Single());
            }
        }
    }
    private sealed record Field(int Id, byte[]? Bytes = null, ulong Number = 0);
    private static List<Field> Parse(byte[] bytes)
    {
        var result = new List<Field>(); var reader = new RouteProtoReader(bytes);
        while (!reader.End)
        {
            var (field, wire) = reader.Key();
            if (wire == 2) result.Add(new(field, reader.Blob()));
            else if (wire == 0) result.Add(new(field, Number: reader.Number()));
            else throw new InvalidDataException("Unexpected fixed contract field");
        }
        return result;
    }
    private static string[] Text(IEnumerable<Field> fields, int id) => fields.Where(field => field.Id == id)
        .Select(field => System.Text.Encoding.UTF8.GetString(field.Bytes!)).ToArray();
}
