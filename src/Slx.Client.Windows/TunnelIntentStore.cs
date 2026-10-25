using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text.Json;
using Slx.Client.Core;
namespace Slx.Client.Windows;
public sealed record ProtectedTunnelIntent(string OwnerSid,ConnectPlan Plan,string? EndpointHost=null);
/// <summary>Service-only resume state; machine DPAPI plus SYSTEM/Administrators-only directory and inherited file ACL.</summary>
public sealed partial class TunnelIntentStore
{
    private readonly string root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"SLX.NetworkService");
    private static readonly byte[] Entropy=SHA256.HashData("SLX.NetworkService.Intent.v1"u8);
    public ProtectedTunnelIntent? Load()
    {
        SecureDirectory();var path=Path.Combine(root,"intent.bin");
        if(!File.Exists(path))return null;
        CheckFile(path);
        if(new FileInfo(path).Length>1048576)throw new ClientFault("PROTECTION_STORAGE_FAILED");
        var encrypted=File.ReadAllBytes(path);byte[]? plaintext=null;
        try
        {
            plaintext=ProtectedData.Unprotect(encrypted,Entropy,DataProtectionScope.LocalMachine);
            var intent=JsonSerializer.Deserialize<ProtectedTunnelIntent?>(plaintext);
            if(intent!=null){_ = new SecurityIdentifier(intent.OwnerSid);PlanValidator.Validate(intent.Plan);if(intent.EndpointHost is not null)EndpointPolicy.ValidateHost(intent.EndpointHost);}
            return intent;
        }
        catch(Exception error) when(error is CryptographicException or JsonException or ArgumentException)
        {throw new ClientFault("PROTECTION_STORAGE_FAILED");}
        finally{if(plaintext!=null)CryptographicOperations.ZeroMemory(plaintext);}
    }
    public void Save(ProtectedTunnelIntent? intent)
    {
        SecureDirectory();
        if(intent!=null){_ = new SecurityIdentifier(intent.OwnerSid);PlanValidator.Validate(intent.Plan);if(intent.EndpointHost is not null)EndpointPolicy.ValidateHost(intent.EndpointHost);}
        byte[] plaintext=JsonSerializer.SerializeToUtf8Bytes(intent);
        var temporary=Path.Combine(root,Guid.NewGuid().ToString("N")+".tmp");
        try
        {
            var encrypted=ProtectedData.Protect(plaintext,Entropy,DataProtectionScope.LocalMachine);
            using(var file=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None,4096,FileOptions.WriteThrough))
            {file.Write(encrypted);file.Flush(true);}
            string destination=Path.Combine(root,"intent.bin");if(File.Exists(destination))CheckFile(destination);
            File.Move(temporary,destination,true);
        }
        finally{CryptographicOperations.ZeroMemory(plaintext);if(File.Exists(temporary))File.Delete(temporary);}
    }
    private void SecureDirectory()
    {
        using var identity=WindowsIdentity.GetCurrent();
        if(!identity.IsSystem&&!new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator))throw new ClientFault("PROTECTION_STORAGE_FAILED");
        var system=new SecurityIdentifier(WellKnownSidType.LocalSystemSid,null);
        var administrators=new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid,null);
        var security=new DirectorySecurity();security.SetAccessRuleProtection(true,false);security.SetOwner(administrators);
        foreach(var sid in new[]{system,administrators})security.AddAccessRule(new(sid,FileSystemRights.FullControl,
            InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit,PropagationFlags.None,AccessControlType.Allow));
        var directory=new DirectoryInfo(root);
        if(directory.Exists){CheckFile(root);directory.SetAccessControl(security);}else directory.Create(security);
    }
    private static void CheckFile(string path)
    {
        if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new ClientFault("PROTECTION_STORAGE_FAILED");
    }
}
