using System.Security.Cryptography;
using System.Security.Principal;
using System.Text.Json;
using System.Text.Json.Serialization;
using Slx.Client.Core;

namespace Slx.Client.Windows;

public sealed record ProtectedUpdateMaintenance(string OwnerSid, Guid SessionId, DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt, bool Claimed);

public interface IUpdateMaintenanceStore
{
    ProtectedUpdateMaintenance? LoadMaintenance();
    void SaveMaintenance(ProtectedUpdateMaintenance? value);
}

public sealed partial class TunnelIntentStore : IUpdateMaintenanceStore
{
    private static readonly byte[] MaintenanceEntropy = SHA256.HashData("SLX.NetworkService.UpdateMaintenance.v1"u8);
    private static readonly JsonSerializerOptions MaintenanceJson = new()
    { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 4 };
    public ProtectedUpdateMaintenance? LoadMaintenance()
    {
        SecureDirectory();
        var path = Path.Combine(root, "update-maintenance.bin");
        if (!File.Exists(path)) return null;
        CheckFile(path);
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (file.Length is <= 0 or > 16384) throw new ClientFault("UPDATE_MAINTENANCE_UNCONFIRMED");
        byte[] encrypted = new byte[(int)file.Length];
        file.ReadExactly(encrypted);
        byte[]? plaintext = null;
        try
        {
            plaintext = ProtectedData.Unprotect(encrypted, MaintenanceEntropy, DataProtectionScope.LocalMachine);
            if (plaintext.Length > 4096) throw new ClientFault("UPDATE_MAINTENANCE_UNCONFIRMED");
            var value = JsonSerializer.Deserialize<ProtectedUpdateMaintenance>(plaintext, MaintenanceJson)
                ?? throw new ClientFault("UPDATE_MAINTENANCE_UNCONFIRMED");
            ValidateMaintenance(value);
            return value;
        }
        catch (Exception error) when (error is CryptographicException or JsonException or ArgumentException)
        { throw new ClientFault("UPDATE_MAINTENANCE_UNCONFIRMED"); }
        finally { if (plaintext is not null) CryptographicOperations.ZeroMemory(plaintext); }
    }
    public void SaveMaintenance(ProtectedUpdateMaintenance? value)
    {
        SecureDirectory();
        string destination = Path.Combine(root, "update-maintenance.bin");
        if (File.Exists(destination)) CheckFile(destination);
        if (value is null) { if (File.Exists(destination)) File.Delete(destination); return; }
        ValidateMaintenance(value);
        byte[] plaintext = JsonSerializer.SerializeToUtf8Bytes(value, MaintenanceJson);
        string temporary = Path.Combine(root, Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            var encrypted = ProtectedData.Protect(plaintext, MaintenanceEntropy, DataProtectionScope.LocalMachine);
            using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            { file.Write(encrypted); file.Flush(true); }
            File.Move(temporary, destination, true);
        }
        finally
        { CryptographicOperations.ZeroMemory(plaintext); if (File.Exists(temporary)) File.Delete(temporary); }
    }
    private static void ValidateMaintenance(ProtectedUpdateMaintenance value)
    {
        _ = new SecurityIdentifier(value.OwnerSid);
        var duration = value.ExpiresAt - value.CreatedAt;
        if (value.SessionId == Guid.Empty || value.CreatedAt.Offset != TimeSpan.Zero || value.ExpiresAt.Offset != TimeSpan.Zero
            || duration <= TimeSpan.Zero || duration > TimeSpan.FromMinutes(value.Claimed ? 15 : 5))
            throw new ClientFault("UPDATE_MAINTENANCE_UNCONFIRMED");
    }
}
