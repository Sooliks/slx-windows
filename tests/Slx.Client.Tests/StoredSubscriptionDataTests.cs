using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Slx.Client.Core;
using Slx.Client.Windows;
using Xunit;
namespace Slx.Client.Tests;

public sealed class StoredSubscriptionDataTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "SLX-SubscriptionData-Tests", Guid.NewGuid().ToString("N"));
    [Theory]
    [InlineData("snapshot-null")]
    [InlineData("profiles-null")]
    [InlineData("profile-null")]
    [InlineData("profiles-empty")]
    [InlineData("profiles-excessive")]
    [InlineData("profile-duplicate")]
    [InlineData("profile-invalid")]
    [InlineData("title-null")]
    [InlineData("url-invalid")]
    public void MalformedDecryptedSnapshotReportsStorageFaultAndPreservesCiphertext(string mutation)
    {
        var store = new UserSettingsStore(directory);
        var id = store.GetOrCreateInstallationId();
        var subscription = new StoredSubscription(WindowsSubscriptionTests.Url, SubscriptionParser.Parse(CoreFixtures.Hysteria));
        var document = JsonSerializer.SerializeToNode(new { InstallationId = id, Subscription = subscription })!;
        var stored = document["Subscription"]!; var snapshot = stored["Snapshot"]!;
        switch (mutation)
        {
            case "snapshot-null": stored["Snapshot"] = null; break;
            case "profiles-null": snapshot["Profiles"] = null; break;
            case "profile-null": snapshot["Profiles"] = new JsonArray((JsonNode?)null); break;
            case "profiles-empty": snapshot["Profiles"] = new JsonArray(); break;
            case "profiles-excessive": snapshot["Profiles"] = new JsonArray(Enumerable.Range(0, 65)
                .Select(_ => snapshot["Profiles"]![0]!.DeepClone()).ToArray()); break;
            case "profile-duplicate": snapshot["Profiles"]!.AsArray().Add(snapshot["Profiles"]![0]!.DeepClone()); break;
            case "profile-invalid": snapshot["Profiles"]![0]!["Host"] = "localhost"; break;
            case "title-null": snapshot["Title"] = null; break;
            case "url-invalid": stored["Url"] = "https://other.example/sub/invalid"; break;
        }
        var plain = Encoding.UTF8.GetBytes(document.ToJsonString());
        var entropy = SHA256.HashData(Encoding.UTF8.GetBytes("SLX.Windows.UserVault.v1"));
        var cipher = ProtectedData.Protect(plain, entropy, DataProtectionScope.CurrentUser);
        var path = Path.Combine(directory, "profile.dat");
        try
        {
            File.WriteAllBytes(path, cipher);
            Assert.Equal("STORAGE_UNAVAILABLE", Assert.Throws<ClientFault>(() => store.LoadSubscription()).Code);
            Assert.Equal(cipher, File.ReadAllBytes(path));
            Assert.Equal(id, store.GetOrCreateInstallationId());
            Assert.Equal(cipher, File.ReadAllBytes(path));
            // An explicit replacement can use the valid existing installation identity; a bad cached snapshot never resets it.
            store.SaveSubscription(subscription);
            Assert.Equal(id, store.GetOrCreateInstallationId()); Assert.NotNull(store.LoadSubscription());
        }
        finally { CryptographicOperations.ZeroMemory(plain); CryptographicOperations.ZeroMemory(cipher); }
    }

    public void Dispose()
    {
        var allowed = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "SLX-SubscriptionData-Tests")) + Path.DirectorySeparatorChar;
        var target = Path.GetFullPath(directory);
        if (target.StartsWith(allowed, StringComparison.OrdinalIgnoreCase) && Directory.Exists(target)) Directory.Delete(target, true);
    }
}
