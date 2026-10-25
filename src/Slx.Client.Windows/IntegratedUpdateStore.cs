using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Slx.Client.Core;

namespace Slx.Client.Windows;

internal static class IntegratedUpdateStore
{
    private static readonly JsonSerializerOptions Json = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 8 };
    internal static void Write<T>(Guid id, string name, T value)
    {
        var directory = IntegratedUpdatePaths.Session(id); IntegratedUpdatePaths.NoReparse(directory);
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(value, Json);
        if (bytes.Length > 32768) throw new ClientFault("UPDATE_INVALID");
        string destination = Path.Combine(directory, name), temporary = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            byte[] protectedBytes = ProtectedData.Protect(bytes, Entropy(id, name), DataProtectionScope.CurrentUser);
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            { output.Write(protectedBytes); output.Flush(true); }
            if (System.IO.File.Exists(destination)) IntegratedUpdatePaths.File(destination);
            System.IO.File.Move(temporary, destination, true);
        }
        finally { CryptographicOperations.ZeroMemory(bytes); if (System.IO.File.Exists(temporary)) System.IO.File.Delete(temporary); }
    }
    internal static T Read<T>(Guid id, string name)
    {
        string path = Path.Combine(IntegratedUpdatePaths.Session(id), name); IntegratedUpdatePaths.File(path);
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (input.Length is <= 0 or > 65536) throw new ClientFault("UPDATE_INVALID");
        var encrypted = new byte[checked((int)input.Length)]; input.ReadExactly(encrypted);
        byte[] bytes = ProtectedData.Unprotect(encrypted, Entropy(id, name), DataProtectionScope.CurrentUser);
        try
        {
            if (bytes.Length > 32768) throw new ClientFault("UPDATE_INVALID");
            return JsonSerializer.Deserialize<T>(bytes, Json) ?? throw new ClientFault("UPDATE_INVALID");
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
    private static byte[] Entropy(Guid id, string name) => SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"SLX.UpdateSession.v1:{id:N}:{name}"));
    internal static void Validate(IntegratedUpdateJob job, Guid id)
    {
        if (job.Offer is null || string.IsNullOrEmpty(job.InstallerPath) || string.IsNullOrEmpty(job.InstallationRoot))
            throw new ClientFault("UPDATE_INVALID");
        UpdateSourcePolicy.Validate(job.Offer);
        if (!ReleaseVersion.TryParse(job.SourceVersion, out _)) throw new ClientFault("UPDATE_INVALID");
        if (job.SessionId != id || job.OriginalProcessId <= 0 || job.CreatedAt.Offset != TimeSpan.Zero
            || job.CreatedAt > DateTimeOffset.UtcNow.AddMinutes(1) || job.CreatedAt < DateTimeOffset.UtcNow.AddHours(-24)
            || job.Language is not ("ru" or "en") || job.ResumeConnection && !IntegratedUpdatePolicy.Fingerprint(job.ResumeFingerprint))
            throw new ClientFault("UPDATE_INVALID");
        _ = IntegratedUpdatePolicy.FileVersion(job.Offer.Version);
        _ = IntegratedUpdatePaths.ValidateInstallation(job.InstallationRoot);
        var path = Path.GetFullPath(job.InstallerPath);
        var expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SLX", "VPN", "Updates");
        var parent = Path.GetDirectoryName(path)!;
        if (Path.GetDirectoryName(parent) != expected || !Guid.TryParseExact(Path.GetFileName(parent), "N", out _)
            || Path.GetFileName(path) != job.Offer.AssetName) throw new ClientFault("UPDATE_INVALID");
        IntegratedUpdatePaths.File(path);
    }
}
