using System.Security.Cryptography;
using System.Text.Json;
using Slx.Client.Core;

namespace Slx.Client.Windows;

public sealed partial class UserSettingsStore
{
    private const int MaxDiscoveryBytes = 1048576;
    private static readonly byte[] DiscoveryEntropy = SHA256.HashData("SLX.Windows.DiscoveryCache.v1"u8);
    private sealed record DiscoveryCache(int SchemaVersion, DateTimeOffset CapturedAt, IReadOnlyList<AppRule> Applications);

    public ApplicationDiscoverySnapshot? LoadDiscoveryCache()
    {
        lock (sync)
        {
            byte[]? cipher = null;
            byte[]? plain = null;
            try
            {
                cipher = ReadBounded("discovery.dat", MaxDiscoveryBytes);
                if (cipher is null) return null;
                plain = ProtectedData.Unprotect(cipher, DiscoveryEntropy, DataProtectionScope.CurrentUser);
                if (plain.Length is <= 0 or > MaxDiscoveryBytes - 4096) throw new ClientFault("STORAGE_UNAVAILABLE");
                var cache = JsonSerializer.Deserialize<DiscoveryCache>(plain);
                if (cache is null || cache.SchemaVersion != 1) throw new ClientFault("STORAGE_UNAVAILABLE");
                var snapshot = new ApplicationDiscoverySnapshot(cache.CapturedAt, cache.Applications);
                DiscoveryCacheValidation.Validate(snapshot);
                return snapshot;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or CryptographicException or JsonException or ArgumentException)
            { throw new ClientFault("STORAGE_UNAVAILABLE"); }
            finally
            {
                if (cipher is not null) CryptographicOperations.ZeroMemory(cipher);
                if (plain is not null) CryptographicOperations.ZeroMemory(plain);
            }
        }
    }

    public void SaveDiscoveryCache(ApplicationDiscoverySnapshot snapshot)
    {
        byte[]? plain = null;
        byte[]? cipher = null;
        try
        {
            DiscoveryCacheValidation.Validate(snapshot);
            plain = JsonSerializer.SerializeToUtf8Bytes(new DiscoveryCache(1, snapshot.CapturedAt, snapshot.Applications));
            if (plain.Length > MaxDiscoveryBytes - 4096) throw new ClientFault("STORAGE_UNAVAILABLE");
            cipher = ProtectedData.Protect(plain, DiscoveryEntropy, DataProtectionScope.CurrentUser);
            Save("discovery.dat", cipher, MaxDiscoveryBytes);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or CryptographicException or JsonException or ArgumentException)
        { throw new ClientFault("STORAGE_UNAVAILABLE"); }
        finally
        {
            if (plain is not null) CryptographicOperations.ZeroMemory(plain);
            if (cipher is not null) CryptographicOperations.ZeroMemory(cipher);
        }
    }
}
