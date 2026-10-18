using Slx.Client.Core;
using Slx.Client.Windows;
using Xunit;
namespace Slx.Client.Tests;
public sealed class ApplicationCatalogTests
{
    [Theory]
    [InlineData("../unsafe.exe")]
    [InlineData("C:\\secret.exe")]
    [InlineData("app.exe --run")]
    [InlineData("\\\\remote\\app.exe")]
    public void CatalogCannotSupplyPathsOrCommandLines(string executable)
    {
        var json=System.Text.Json.JsonSerializer.Serialize(new{schemaVersion=1,applications=new[]{new{id="app",name="App",category="browser",executables=new[]{executable}}}});
        Assert.Equal("INVALID_CATALOG",Assert.Throws<ClientFault>(()=>ApplicationCatalog.Parse(json)).Code);
    }
    [Fact]
    public void OfflineCatalogWorksWithoutNetworkAndHasKnownApplicationNames()
    {
        var items=CatalogClient.Bundled();
        Assert.Equal(18,items.Count);Assert.Contains(items,x=>x.Id=="chrome");Assert.Contains(items,x=>x.Id=="telegram");
        Assert.Equal(items.Count,items.Select(x=>x.Id).Distinct().Count());
    }
    [Theory]
    [InlineData("")]
    [InlineData("not-an-interface")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void SourcePolicyRejectsInvalidOwnedAdapterWithoutNetworkChanges(string id)
    {
        Assert.Equal("TUN_ADDRESS_FAILED",Assert.Throws<ClientFault>(()=>TunnelSourcePolicy.Apply(id)).Code);
    }
}
