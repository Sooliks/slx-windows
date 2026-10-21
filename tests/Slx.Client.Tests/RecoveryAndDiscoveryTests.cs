using Slx.Client.Core;using Slx.Client.Windows;using Xunit;
namespace Slx.Client.Tests;
public sealed class RecoveryAndDiscoveryTests
{
    [Theory]
    [InlineData(0,2)][InlineData(1,2)][InlineData(2,5)][InlineData(3,10)][InlineData(4,20)][InlineData(5,40)][InlineData(6,60)][InlineData(1000000,60)]
    public void RecoveryNeverExhaustsAndHasBoundedPauses(int failures,int seconds)=>Assert.Equal(TimeSpan.FromSeconds(seconds),RecoveryBackoff.Delay(failures));
    [Theory]
    [InlineData(@"C:\ProgramData\Package Cache\{id}\dotnet-sdk.exe")]
    [InlineData(@"c:\programdata\package cache\vendor\browser.exe")]
    [InlineData(@"C:/ProgramData/Package Cache/msedge.exe")]
    public void PackageCacheIsNotAnApplicationLibrary(string path)=>Assert.False(LibraryApplication.IsCandidate(path));
    [Theory]
    [InlineData(@"C:\ProgramData\Package Cache Else\app.exe")]
    [InlineData(@"C:\Apps\Package Cache\app.exe")]
    public void CacheBoundaryDoesNotHideOtherPrograms(string path)=>Assert.True(LibraryApplication.IsCandidate(path));
    [Fact]
    public void OrdinaryApplicationsDoNotGainUnrelatedHelpers()
    {
        var app=CoreFixtures.App();var expanded=ApplicationGroups.Expand([app,app with{Id=Guid.NewGuid(),Enabled=false}]);
        Assert.Single(expanded);Assert.Equal(app.ExecutablePath,expanded[0].ExecutablePath);Assert.True(expanded[0].Enabled);
    }
    [Fact]
    public void CatalogMakesTheCombinedDesktopGroupSearchableByBothNames()
    {
        var group=Assert.Single(CatalogClient.Bundled(),x=>x.Id=="openai-desktop");
        Assert.Contains("Codex",group.Name);Assert.Contains("ChatGPT",group.Name);
        Assert.Contains("ChatGPT.exe",group.Executables);Assert.Contains("codex.exe",group.Executables);
    }
    [Fact]
    public void PublicDnsCannotFallBackToPlainUdpOrBypassViaSelfRule()
    {
        using var json=System.Text.Json.JsonDocument.Parse(XrayConfigBuilder.Build(CoreFixtures.Plan(),18899,"probeUser00000000","probePassword0000"));
        var root=json.RootElement;Assert.All(root.GetProperty("dns").GetProperty("servers").EnumerateArray(),x=>Assert.StartsWith("https://",x.GetString()));
        Assert.True(root.GetProperty("dns").GetProperty("disableFallback").GetBoolean());
        var rules=root.GetProperty("routing").GetProperty("rules").EnumerateArray().ToArray();
        int resolver=Array.FindIndex(rules,x=>x.TryGetProperty("inboundTag",out var tags)&&tags.EnumerateArray().Any(t=>t.GetString()=="resolver"));
        int self=Array.FindIndex(rules,x=>x.TryGetProperty("process",out _));Assert.InRange(resolver,0,self-1);
        Assert.Equal("vpn",rules[resolver].GetProperty("outboundTag").GetString());
        Assert.Equal("dns",Assert.Single(rules,x=>x.TryGetProperty("port",out var port)&&port.GetString()=="53").GetProperty("outboundTag").GetString());
    }
}
