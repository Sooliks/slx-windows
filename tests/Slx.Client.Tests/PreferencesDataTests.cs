using System.Text.Json;
using Slx.Client.Core;
using Slx.Client.Windows;
using Xunit;
namespace Slx.Client.Tests;

public sealed class PreferencesDataTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "SLX-Preferences-Tests", Guid.NewGuid().ToString("N"));
    private string PathName => Path.Combine(directory, "preferences.json");
    private UserSettingsStore Store => new(directory);

    [Theory]
    [InlineData("null")]
    [InlineData("{\"Language\":null}")]
    [InlineData("{\"Language\":\"fr\"}")]
    [InlineData("{\"Theme\":null}")]
    [InlineData("{\"Mode\":99}")]
    [InlineData("{\"Applications\":[null]}")]
    [InlineData("{\"Applications\":[{\"Name\":null,\"ExecutablePath\":null}]}")]
    public void InvalidValidJsonPreferencesFailClosedWithoutRewriting(string json)
    {
        Directory.CreateDirectory(directory); File.WriteAllText(PathName, json);
        var store = Store;
        Assert.Equal("STORAGE_UNAVAILABLE", Assert.Throws<ClientFault>(() => store.LoadPreferences()).Code);
        Assert.Equal("STORAGE_UNAVAILABLE", Assert.Throws<ClientFault>(() => store.SavePreferences(new ClientPreferences(Language: "en"))).Code);
        Assert.Equal(json, File.ReadAllText(PathName));
        Assert.Single(Directory.GetFiles(directory));
    }

    [Fact]
    public void NewSessionMaySaveAfterTheOwnerRestoresTheSettingsFile()
    {
        Directory.CreateDirectory(directory); File.WriteAllText(PathName, "null");
        var failed = Store; Assert.Throws<ClientFault>(() => failed.LoadPreferences());
        File.WriteAllText(PathName, "{}");
        Assert.Throws<ClientFault>(() => failed.SavePreferences(new ClientPreferences(Language: "en")));
        var next = Store; next.LoadPreferences(); next.SavePreferences(new ClientPreferences(Language: "en"));
        Assert.Equal("en", Store.LoadPreferences().Language);
    }

    [Fact]
    public void LegacyEmptyPreferencesAndMissingExecutablesRetainTheirExactIntent()
    {
        Directory.CreateDirectory(directory); File.WriteAllText(PathName, "{}");
        Assert.Equal(new ClientPreferences(), Store.LoadPreferences());
        var rule = new AppRule(Guid.NewGuid(), "Uninstalled app", @"Z:\Previous installation\Uninstalled.exe", false);
        var expected = new ClientPreferences(Applications: [rule]);
        Store.SavePreferences(expected);
        Assert.Equal(rule, Assert.Single(Store.LoadPreferences().Applications!));
        Assert.DoesNotContain("profile.dat", Directory.GetFiles(directory).Select(Path.GetFileName));
    }

    [Fact]
    public void OversizedOrMalformedApplicationIntentIsRejected()
    {
        foreach (var apps in new[]
        {
            new List<AppRule> { CoreFixtures.App() with { Id = Guid.Empty } },
            [CoreFixtures.App() with { Name = "\r\n" }],
            [CoreFixtures.App() with { ExecutablePath = @"\\server\app.exe" }],
            [CoreFixtures.App(), CoreFixtures.App() with { ExecutablePath = CoreFixtures.App().ExecutablePath.ToUpperInvariant() }],
            Enumerable.Range(0, 129).Select(index => CoreFixtures.App() with { ExecutablePath = $@"C:\Synthetic\App{index}.exe" }).ToList()
        })
        {
            Directory.CreateDirectory(directory);
            var json = JsonSerializer.Serialize(new ClientPreferences(Applications: apps)); File.WriteAllText(PathName, json);
            Assert.Equal("STORAGE_UNAVAILABLE", Assert.Throws<ClientFault>(() => Store.LoadPreferences()).Code);
            Assert.Equal(json, File.ReadAllText(PathName));
        }
    }

    public void Dispose()
    {
        var expected = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "SLX-Preferences-Tests")) + Path.DirectorySeparatorChar;
        var target = Path.GetFullPath(directory);
        if (target.StartsWith(expected, StringComparison.OrdinalIgnoreCase) && Directory.Exists(target)) Directory.Delete(target, true);
    }
}
