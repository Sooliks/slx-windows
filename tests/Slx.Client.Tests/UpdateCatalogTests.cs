using Slx.Client.Core;
using Xunit;

namespace Slx.Client.Tests;

public sealed class UpdateCatalogTests
{
    [Fact]
    public void PicksSemanticallyNewestReleaseRegardlessOfPublicationOrder()
    {
        var json = UpdateFixtures.Feed(UpdateFixtures.Release("0.1.0-alpha.9"), UpdateFixtures.Release("0.1.0-alpha.12"),
            UpdateFixtures.Release("0.1.0-alpha.11"));
        Assert.Equal("0.1.0-alpha.12", UpdateReleaseCatalog.Latest(json, "0.1.0-alpha.10")!.Installer.Version);
    }
    [Fact]
    public void StableInstallDoesNotEnterPrereleaseAndNoDowngradeIsOffered()
    {
        Assert.Null(UpdateReleaseCatalog.Latest(UpdateFixtures.Feed(UpdateFixtures.Release("0.2.0-alpha.1")), "0.1.0"));
        Assert.Null(UpdateReleaseCatalog.Latest(UpdateFixtures.Feed(UpdateFixtures.Release()), "0.1.0-alpha.12"));
        Assert.Null(UpdateReleaseCatalog.Latest(UpdateFixtures.Feed(UpdateFixtures.Release()), UpdateFixtures.Version));
    }
    [Theory]
    [InlineData("draft")]
    [InlineData("cross-repository")]
    [InlineData("cross-tag")]
    [InlineData("no-checksum")]
    [InlineData("duplicate")]
    [InlineData("wrong-size-type")]
    [InlineData("oversize")]
    [InlineData("page-fragment")]
    public void UntrustedOrIncompleteReleaseCannotBecomeAnOffer(string kind)
    {
        var release = UpdateFixtures.Release();
        var assets = (Dictionary<string, object?>[])release["assets"]!;
        switch (kind)
        {
            case "draft": release["draft"] = true; break;
            case "cross-repository": assets[0]["browser_download_url"] = "https://github.com/attacker/repo/releases/download/v0.1.0/setup.exe"; break;
            case "cross-tag": assets[0]["browser_download_url"] = UpdateFixtures.AssetUrl("0.1.0-alpha.9", UpdateSourcePolicy.InstallerName(UpdateFixtures.Version)).AbsoluteUri; break;
            case "no-checksum": release["assets"] = new[] { assets[0] }; break;
            case "duplicate": release["assets"] = new[] { assets[0], assets[0], assets[1] }; break;
            case "wrong-size-type": assets[0]["size"] = "large"; break;
            case "oversize": assets[0]["size"] = UpdateSourcePolicy.MaxInstallerBytes + 1; break;
            case "page-fragment": release["html_url"] += "#fragment"; break;
        }
        Assert.Null(UpdateReleaseCatalog.Latest(UpdateFixtures.Feed(release), "0.1.0-alpha.10"));
    }
    [Theory]
    [InlineData("http://github.com/Sooliks/slx-windows/releases/download/v0.1.0-alpha.11/SLX-VPN-0.1.0-alpha.11-win-x64-setup.exe")]
    [InlineData("https://github.com.evil.example/Sooliks/slx-windows/releases/download/v0.1.0-alpha.11/SLX-VPN-0.1.0-alpha.11-win-x64-setup.exe")]
    [InlineData("https://user@github.com/Sooliks/slx-windows/releases/download/v0.1.0-alpha.11/SLX-VPN-0.1.0-alpha.11-win-x64-setup.exe")]
    public void ForgedOfferCannotChooseTransportOrAnotherExecutable(string url)
    {
        var offer = UpdateFixtures.Offer() with { InstallerUrl = new(url) };
        Assert.Equal("UPDATE_INVALID", Assert.Throws<ClientFault>(() => UpdateSourcePolicy.Validate(offer)).Code);
    }
    [Fact]
    public void ChecksumRequiresAnExactUniqueInstallerName()
    {
        var name = UpdateSourcePolicy.InstallerName(UpdateFixtures.Version);
        Assert.Equal(UpdateFixtures.Digest, UpdateReleaseCatalog.Checksum(UpdateFixtures.Checksums(), name));
        foreach (var body in new[] { UpdateFixtures.Checksums() + UpdateFixtures.Checksums(), UpdateFixtures.Checksums().Replace(name, "other.exe"),
            UpdateFixtures.Checksums().Replace("  ", " *"), "<html>error</html>" })
            Assert.Equal("UPDATE_INVALID", Assert.Throws<ClientFault>(() => UpdateReleaseCatalog.Checksum(body, name)).Code);
    }
}
