using System.Text;
using Slx.Client.Core;
using Slx.Client.Windows;
using Xunit;

namespace Slx.Client.Tests;

public sealed class WindowsStorageTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "SLX-Windows-Tests", Guid.NewGuid().ToString("N"));
    private UserSettingsStore Store() => new(directory);
    private static StoredSubscription Subscription() => new(WindowsSubscriptionTests.Url,
        SubscriptionParser.Parse(CoreFixtures.Hysteria + "\n" + CoreFixtures.Xhttp));

    [Fact]
    public void DpapiRoundTripsWithoutWritingCredentialsOrSubscriptionUrlInPlaintext()
    {
        var store = Store();
        var subscription = Subscription();
        store.SaveSubscription(subscription);
        var actual = Store().LoadSubscription();
        Assert.NotNull(actual);
        Assert.Equal(subscription.Url, actual.Url);
        Assert.Equal(subscription.Snapshot.Profiles, actual.Snapshot.Profiles);
        var cipher = File.ReadAllBytes(Path.Combine(directory, "profile.dat"));
        foreach (var encoding in new[] { Encoding.UTF8, Encoding.Unicode })
        {
            var visible = encoding.GetString(cipher);
            Assert.DoesNotContain(CoreFixtures.Token, visible);
            Assert.DoesNotContain(subscription.Url, visible);
            Assert.DoesNotContain(store.GetOrCreateInstallationId(), visible);
        }
        Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
    }

    [Fact]
    public void ClearSubscriptionPreservesTheSameInstallationIdAcrossStoreInstances()
    {
        var store = Store();
        var id = store.GetOrCreateInstallationId();
        Assert.Matches("\\Aslxw1-[0-9a-f]{64}\\z", id);
        store.SaveSubscription(Subscription());
        Store().ClearSubscription();
        Assert.Null(Store().LoadSubscription());
        Assert.Equal(id, Store().GetOrCreateInstallationId());
        Assert.True(File.Exists(Path.Combine(directory, "profile.dat")));
    }

    [Fact]
    public async Task ConcurrentStoreInstancesCannotAllocateDifferentOwnIds()
    {
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() => Store().GetOrCreateInstallationId())));
        Assert.Single(results.Distinct());
        var otherDirectory = Path.Combine(directory, "another-installation");
        Assert.NotEqual(results[0], new UserSettingsStore(otherDirectory).GetOrCreateInstallationId());
    }

    [Fact]
    public void PreferencesRoundTripWithoutIncludingProfileSecretsOrChangingTheVault()
    {
        var store = Store();
        var id = store.GetOrCreateInstallationId();
        var preferences = new ClientPreferences("en", "terminal", RoutingMode.WholeComputer,
            [CoreFixtures.App()], AutoStart: true, SelectedProfileId: "synthetic");
        store.SavePreferences(preferences);
        var actual = Store().LoadPreferences();
        Assert.Equal(preferences.Language, actual.Language);
        Assert.Equal(preferences.Theme, actual.Theme);
        Assert.Equal(preferences.Applications, actual.Applications);
        Assert.Equal(id, store.GetOrCreateInstallationId());
        Assert.Null(store.LoadSubscription());
        Assert.DoesNotContain(CoreFixtures.Token, File.ReadAllText(Path.Combine(directory, "preferences.json")));
    }

    [Theory]
    [InlineData("profile.dat", 1048577)]
    [InlineData("preferences.json", 262145)]
    public void OversizeVaultAndPreferencesFailClosedBeforeReadingTheirContents(string name, int length)
    {
        Directory.CreateDirectory(directory);
        using (var stream = File.Create(Path.Combine(directory, name))) stream.SetLength(length);
        var error = Assert.Throws<ClientFault>(() =>
        {
            if (name == "profile.dat") Store().GetOrCreateInstallationId(); else Store().LoadPreferences();
        });
        Assert.Equal("STORAGE_UNAVAILABLE", error.Code);
    }

    [Fact]
    public void CorruptedVaultDoesNotSilentlyResetTheBoundInstallationId()
    {
        var store = Store();
        store.GetOrCreateInstallationId();
        var path = Path.Combine(directory, "profile.dat");
        byte[] corrupt = [1, 2, 3, 4, 5];
        File.WriteAllBytes(path, corrupt);
        Assert.Equal("STORAGE_UNAVAILABLE", Assert.Throws<ClientFault>(() => Store().GetOrCreateInstallationId()).Code);
        Assert.Equal(corrupt, File.ReadAllBytes(path));
    }

    [Fact]
    public void FailedAtomicSaveRemovesItsTemporaryFileAndReportsSafeFault()
    {
        Directory.CreateDirectory(Path.Combine(directory, "preferences.json"));
        var error = Assert.Throws<ClientFault>(() => Store().SavePreferences(new ClientPreferences()));
        Assert.Equal("STORAGE_UNAVAILABLE", error.Code);
        Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
    }

    public void Dispose()
    {
        var expected = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "SLX-Windows-Tests")) + Path.DirectorySeparatorChar;
        var target = Path.GetFullPath(directory);
        if (target.StartsWith(expected, StringComparison.OrdinalIgnoreCase) && Directory.Exists(target)) Directory.Delete(target, true);
    }
}
