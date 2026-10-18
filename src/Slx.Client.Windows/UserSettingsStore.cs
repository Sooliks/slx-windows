using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Slx.Client.Core;

namespace Slx.Client.Windows;

public sealed class UserSettingsStore
{
    private const int MaxVaultBytes = 1048576;
    private const int MaxPreferencesBytes = 262144;
    private readonly string root;
    private readonly string mutexName;
    private readonly object sync = new();
    private static readonly byte[] Entropy = SHA256.HashData(Encoding.UTF8.GetBytes("SLX.Windows.UserVault.v1"));
    private sealed record Vault(string InstallationId, StoredSubscription? Subscription);
    public UserSettingsStore() : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SLX", "VPN")) { }
    internal UserSettingsStore(string directory)
    {
        root = Path.GetFullPath(directory);
        mutexName = @"Local\SLX.VPN.Vault." + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(root.ToUpperInvariant())));
    }

    public ClientPreferences LoadPreferences()
    {
        lock (sync)
        {
            try
            {
                var bytes = ReadBounded("preferences.json", MaxPreferencesBytes);
                return bytes is null ? new() : JsonSerializer.Deserialize<ClientPreferences>(bytes) ?? new();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            { throw new ClientFault("STORAGE_UNAVAILABLE"); }
        }
    }
    public void SavePreferences(ClientPreferences preferences) =>
        Save("preferences.json", JsonSerializer.SerializeToUtf8Bytes(preferences), MaxPreferencesBytes);
    public StoredSubscription? LoadSubscription() => WithVaultLock(() => ReadVault()?.Subscription);
    public string GetOrCreateInstallationId() => WithVaultLock(GetOrCreateIdCore);
    public void SaveSubscription(StoredSubscription subscription) => WithVaultLock(() =>
    {
        SaveVault(new(GetOrCreateIdCore(), subscription)); return true;
    });
    public void ClearSubscription() => WithVaultLock(() =>
    {
        SaveVault(new(GetOrCreateIdCore(), null)); return true;
    });

    private string GetOrCreateIdCore()
    {
        var current = ReadVault();
        if (current is not null) return current.InstallationId;
        var id = "slxw1-" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        SaveVault(new(id, null)); return id;
    }
    private T WithVaultLock<T>(Func<T> action)
    {
        lock (sync)
        {
            Mutex? mutex = null;
            var held = false;
            try
            {
                mutex = new Mutex(false, mutexName);
                try { held = mutex.WaitOne(TimeSpan.FromSeconds(5)); }
                catch (AbandonedMutexException) { held = true; }
                if (!held) throw new ClientFault("STORAGE_UNAVAILABLE");
                return action();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException or JsonException or WaitHandleCannotBeOpenedException)
            { throw new ClientFault("STORAGE_UNAVAILABLE"); }
            finally { if (held) mutex!.ReleaseMutex(); mutex?.Dispose(); }
        }
    }
    private Vault? ReadVault()
    {
        var cipher = ReadBounded("profile.dat", MaxVaultBytes);
        if (cipher is null) return null;
        byte[]? plain = null;
        try
        {
            plain = ProtectedData.Unprotect(cipher, Entropy, DataProtectionScope.CurrentUser);
            var vault = JsonSerializer.Deserialize<Vault>(plain) ?? throw new ClientFault("STORAGE_UNAVAILABLE");
            if (vault.InstallationId is not { Length: 70 } || !vault.InstallationId.StartsWith("slxw1-", StringComparison.Ordinal)
                || !vault.InstallationId[6..].All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f'))
                throw new ClientFault("STORAGE_UNAVAILABLE");
            return vault;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(cipher);
            if (plain is not null) CryptographicOperations.ZeroMemory(plain);
        }
    }
    private void SaveVault(Vault value)
    {
        var plain = JsonSerializer.SerializeToUtf8Bytes(value);
        byte[]? cipher = null;
        try
        {
            if (plain.Length > MaxVaultBytes - 4096) throw new ClientFault("STORAGE_UNAVAILABLE");
            cipher = ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser);
            Save("profile.dat", cipher, MaxVaultBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
            if (cipher is not null) CryptographicOperations.ZeroMemory(cipher);
        }
    }
    private byte[]? ReadBounded(string name, int maximum)
    {
        try
        {
            using var stream = new FileStream(Path.Combine(root, name), FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length is <= 0 || stream.Length > maximum) throw new ClientFault("STORAGE_UNAVAILABLE");
            var bytes = new byte[checked((int)stream.Length)];
            stream.ReadExactly(bytes);
            return bytes;
        }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }
    private void Save(string name, byte[] content, int maximum)
    {
        lock (sync)
        {
            if (content.Length > maximum) throw new ClientFault("STORAGE_UNAVAILABLE");
            string? temporary = null;
            try
            {
                Directory.CreateDirectory(root);
                temporary = Path.Combine(root, "." + Guid.NewGuid().ToString("N") + ".tmp");
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                { stream.Write(content); stream.Flush(true); }
                File.Move(temporary, Path.Combine(root, name), true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { throw new ClientFault("STORAGE_UNAVAILABLE"); }
            finally
            {
                if (temporary is not null)
                {
                    try { File.Delete(temporary); }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
                }
            }
        }
    }
}
