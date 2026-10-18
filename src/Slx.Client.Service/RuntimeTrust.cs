using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text.Json;
using Slx.Client.Core;

namespace Slx.Client.Service;

internal static class RuntimeTrust
{
    private sealed record Manifest(string XrayVersion, ManifestFile[] Files);
    private sealed record ManifestFile(string Name, string Sha256);
    private static readonly HashSet<string> TrustedWriters = new(StringComparer.Ordinal)
    { "S-1-5-18", "S-1-5-32-544", "S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464" };

    public static string Verify()
    {
        string root = Path.GetFullPath(AppContext.BaseDirectory);
        string programFiles = Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
        if (!root.StartsWith(programFiles + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new ClientFault("RUNTIME_INSTALL_LOCATION");
        CheckPath(root, programFiles);
        string runtime = Path.Combine(root, "runtime");
        CheckPath(runtime, programFiles);
        string manifestPath = Path.Combine(runtime, "runtime-manifest.json");
        CheckPath(manifestPath, programFiles);
        Manifest? manifest;
        try
        {
            if (new FileInfo(manifestPath).Length > 16384) throw new ClientFault("RUNTIME_MANIFEST_INVALID");
            manifest = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(manifestPath),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception error) when (error is IOException or JsonException)
        { throw new ClientFault("RUNTIME_MANIFEST_INVALID"); }
        if (manifest?.XrayVersion != "26.9.9" || manifest.Files is null || manifest.Files.Length != 2 || manifest.Files.Any(file => file is null)
            || !manifest.Files.Select(file => file.Name).Order().SequenceEqual(new[] { "wintun.dll", "xray.exe" }))
            throw new ClientFault("RUNTIME_MANIFEST_INVALID");
        foreach (var file in manifest.Files)
        {
            string path = Path.Combine(runtime, file.Name);
            CheckPath(path, programFiles);
            if (file.Sha256 is null || file.Sha256.Length != 64 || !file.Sha256.All(Uri.IsHexDigit))
                throw new ClientFault("RUNTIME_MANIFEST_INVALID");
            using var stream = File.OpenRead(path);
            if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(stream), Convert.FromHexString(file.Sha256)))
                throw new ClientFault("RUNTIME_HASH_MISMATCH");
        }
        return Path.Combine(runtime, "xray.exe");
    }

    private static void CheckPath(string path, string boundary)
    {
        FileSystemInfo? item = Directory.Exists(path) ? new DirectoryInfo(path) : new FileInfo(path);
        while (item is not null && item.FullName.Length >= boundary.Length)
        {
            if (!item.Exists || (item.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new ClientFault("RUNTIME_UNSAFE_PATH");
            var security = item is DirectoryInfo directory
                ? (FileSystemSecurity)directory.GetAccessControl() : ((FileInfo)item).GetAccessControl();
            if (!TrustedWriters.Contains(security.GetOwner(typeof(SecurityIdentifier))?.Value ?? ""))
                throw new ClientFault("RUNTIME_UNSAFE_OWNER");
            foreach (FileSystemAccessRule rule in security.GetAccessRules(true, true, typeof(SecurityIdentifier)))
            {
                const FileSystemRights writes = FileSystemRights.Write | FileSystemRights.Delete |
                    FileSystemRights.ChangePermissions | FileSystemRights.TakeOwnership | FileSystemRights.DeleteSubdirectoriesAndFiles;
                bool canWrite = (rule.FileSystemRights & writes) != 0 || ((uint)rule.FileSystemRights & 0x50000000) != 0;
                if ((rule.PropagationFlags & PropagationFlags.InheritOnly) == 0
                    && rule.AccessControlType == AccessControlType.Allow && canWrite
                    && !TrustedWriters.Contains(rule.IdentityReference.Value))
                    throw new ClientFault("RUNTIME_UNSAFE_ACL");
            }
            item = item is DirectoryInfo parent ? parent.Parent : ((FileInfo)item).Directory;
        }
    }
}
