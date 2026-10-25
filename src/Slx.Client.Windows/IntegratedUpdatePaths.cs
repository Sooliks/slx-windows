using System.Diagnostics;
using System.Security.AccessControl;
using System.Security.Principal;
using Slx.Client.Core;

namespace Slx.Client.Windows;

internal static class IntegratedUpdatePaths
{
    internal const string HostName = "SLX VPN Updater.exe";
    internal static string Root => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SLX", "VPN", "UpdateSessions");
    internal static string Session(Guid id) => id == Guid.Empty ? throw new ClientFault("UPDATE_INVALID") : Path.Combine(Root, id.ToString("N"));
    internal static void NoReparse(string path)
    {
        for (var directory = new DirectoryInfo(path); directory is not null; directory = directory.Parent)
            if ((directory.Attributes & FileAttributes.ReparsePoint) != 0) throw new ClientFault("UPDATE_INVALID");
    }
    internal static void File(string path)
    {
        NoReparse(Path.GetDirectoryName(path)!);
        if ((System.IO.File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new ClientFault("UPDATE_INVALID");
    }
    internal static void Create(Guid id)
    {
        Directory.CreateDirectory(Root); NoReparse(Root);
        var security = new DirectorySecurity(); security.SetAccessRuleProtection(true, false);
        using var identity = WindowsIdentity.GetCurrent();
        foreach (var sid in new[] { identity.User ?? throw new ClientFault("UPDATE_INVALID"),
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null) })
            security.AddAccessRule(new(sid, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None, AccessControlType.Allow));
        if (Directory.Exists(Session(id))) throw new ClientFault("UPDATE_SESSION_EXISTS");
        new DirectoryInfo(Session(id)).Create(security); NoReparse(Session(id));
    }
    internal static string ValidateInstallation(string path)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)); NoReparse(root);
        var programFiles = Path.TrimEndingDirectorySeparator(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
        if (!root.StartsWith(programFiles + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new ClientFault("UPDATE_INSTALLATION_UNSUPPORTED");
        var allowed = new[] { "S-1-5-18", "S-1-5-32-544", "S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464" };
        var security = new DirectoryInfo(root).GetAccessControl();
        if (security.GetOwner(typeof(SecurityIdentifier)) is not SecurityIdentifier owner || !allowed.Contains(owner.Value))
            throw new ClientFault("UPDATE_INSTALLATION_UNSUPPORTED");
        foreach (FileSystemAccessRule rule in security.GetAccessRules(true, true, typeof(SecurityIdentifier)))
            if (rule.AccessControlType == AccessControlType.Allow && !allowed.Contains(rule.IdentityReference.Value)
                && (rule.FileSystemRights & (FileSystemRights.WriteData | FileSystemRights.AppendData | FileSystemRights.Delete | FileSystemRights.DeleteSubdirectoriesAndFiles
                    | FileSystemRights.ChangePermissions | FileSystemRights.TakeOwnership)) != 0)
                throw new ClientFault("UPDATE_INSTALLATION_UNSUPPORTED");
        File(Path.Combine(root, "SLX VPN.exe")); File(Path.Combine(root, "service", "SLX.Service.exe"));
        return root;
    }
    internal static string ReadyName(Guid id)
    {
        using var identity = WindowsIdentity.GetCurrent(); using var process = Process.GetCurrentProcess();
        var user = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(identity.User!.Value)));
        return $@"Local\SLX.VPN.Update.{user}.{process.SessionId}.{id:N}.ready";
    }
}
