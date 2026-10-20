using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Slx.Client.Core;
using Slx.Client.Windows;
using Xunit;

namespace Slx.Client.Tests;

public sealed partial class DiscoveryCacheTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "SLX-Discovery-Tests", Guid.NewGuid().ToString("N"));
    private string CachePath => Path.Combine(directory, "discovery.dat");
    private UserSettingsStore Store() => new(directory);
    private static AppRule App(string name = "Пример", string path = @"C:\SLX-Cache-Fixture\missing\sample.exe") => new(Guid.NewGuid(), name, path);
    private static ApplicationDiscoverySnapshot Snapshot(params AppRule[] applications) => new(DateTimeOffset.UtcNow, applications);

    [Fact]
    public void MissingCacheReturnsNothingWithoutCreatingAnyFilesOrInstallationIdentity()
    {
        Assert.Null(Store().LoadDiscoveryCache());
        Assert.False(Directory.Exists(directory));
    }

    [Fact]
    public void DpapiSnapshotRoundTripsWithoutInspectingWhetherExecutableStillExists()
    {
        var snapshot = Snapshot(App(), App("Second", @"D:\SLX-Cache-Fixture\second.exe") with { Enabled = false });
        Store().SaveDiscoveryCache(snapshot);
        var restored = Store().LoadDiscoveryCache();
        Assert.NotNull(restored);
        Assert.Equal(snapshot.CapturedAt, restored.CapturedAt);
        Assert.Equal(snapshot.Applications, restored.Applications);
        Assert.Single(Directory.GetFiles(directory));
        var cipher = File.ReadAllBytes(CachePath);
        foreach (var encoding in new[] { Encoding.UTF8, Encoding.Unicode })
        {
            var visible = encoding.GetString(cipher);
            Assert.DoesNotContain(snapshot.Applications[0].Name, visible);
            Assert.DoesNotContain(snapshot.Applications[0].ExecutablePath, visible);
        }
        Assert.DoesNotContain(snapshot.Applications[0].ExecutablePath, restored.ToString());
    }

    [Fact]
    public void EmptyAndMaximumSnapshotsAreValidAndReplacementLeavesNoTemporaryFile()
    {
        Store().SaveDiscoveryCache(Snapshot());
        Assert.Empty(Store().LoadDiscoveryCache()!.Applications);
        var rows = Enumerable.Range(0, 300).Select(index => App("App " + index, @"C:\SLX-Cache-Fixture\app" + index + ".exe")).ToArray();
        Store().SaveDiscoveryCache(Snapshot(rows));
        Assert.Equal(300, Store().LoadDiscoveryCache()!.Applications.Count);
        Assert.Single(Directory.GetFiles(directory));
    }

    [Fact]
    public void CacheAndCacheCorruptionCannotAlterTheExistingVaultOrPreferences()
    {
        var store = Store();
        var id = store.GetOrCreateInstallationId();
        store.SaveSubscription(new(WindowsSubscriptionTests.Url, SubscriptionParser.Parse(CoreFixtures.Hysteria)));
        store.SavePreferences(new("en", "terminal"));
        var vault = File.ReadAllBytes(Path.Combine(directory, "profile.dat"));
        var preferences = File.ReadAllBytes(Path.Combine(directory, "preferences.json"));
        store.SaveDiscoveryCache(Snapshot(App()));
        File.WriteAllBytes(CachePath, [1, 2, 3, 4]);
        Fault(() => Store().LoadDiscoveryCache());
        Assert.Equal(id, Store().GetOrCreateInstallationId());
        Assert.Equal(WindowsSubscriptionTests.Url, Store().LoadSubscription()!.Url);
        Assert.Equal("terminal", Store().LoadPreferences().Theme);
        Assert.Equal(vault, File.ReadAllBytes(Path.Combine(directory, "profile.dat")));
        Assert.Equal(preferences, File.ReadAllBytes(Path.Combine(directory, "preferences.json")));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, File.ReadAllBytes(CachePath));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1048577)]
    public void InvalidCipherSizeFailsBeforeDecrypting(int length)
    {
        Directory.CreateDirectory(directory);
        using (var stream = File.Create(CachePath)) stream.SetLength(length);
        Fault(() => Store().LoadDiscoveryCache());
    }

    [Fact]
    public void OtherDpapiPurposeCannotBeReadAsAnApplicationCache()
    {
        Directory.CreateDirectory(directory);
        var bytes = ProtectedData.Protect("{}"u8.ToArray(), SHA256.HashData("SLX.Windows.UserVault.v1"u8), DataProtectionScope.CurrentUser);
        File.WriteAllBytes(CachePath, bytes);
        Fault(() => Store().LoadDiscoveryCache());
    }

    [Fact]
    public void AtomicSaveFailureIsRecoverableAndCleansItsTemporaryFile()
    {
        Directory.CreateDirectory(CachePath);
        Fault(() => Store().SaveDiscoveryCache(Snapshot(App())));
        Assert.Empty(Directory.GetFiles(directory));
    }

    private static void Fault(Action action) => Assert.Equal("STORAGE_UNAVAILABLE", Assert.Throws<ClientFault>(action).Code);
    private void WriteEnvelope(object value)
    {
        Directory.CreateDirectory(directory);
        var plain = JsonSerializer.SerializeToUtf8Bytes(value);
        try
        {
            var cipher = ProtectedData.Protect(plain, SHA256.HashData("SLX.Windows.DiscoveryCache.v1"u8), DataProtectionScope.CurrentUser);
            File.WriteAllBytes(CachePath, cipher);
        }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }

    public void Dispose()
    {
        var expected = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "SLX-Discovery-Tests")) + Path.DirectorySeparatorChar;
        var target = Path.GetFullPath(directory);
        if (!target.StartsWith(expected, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("TEMP_SCOPE");
        if (Directory.Exists(target)) Directory.Delete(target, true);
    }
}
