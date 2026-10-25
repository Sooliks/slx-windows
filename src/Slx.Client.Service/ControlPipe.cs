using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using Slx.Client.Core;
using Slx.Client.Windows;

namespace Slx.Client.Service;

internal static class ControlPipe
{
    internal const int MaxInstances = 4;
    internal sealed record CallerRequest(string Sid, PipeRequest Request, bool IsAdministrator = false);

    public static async Task<CallerRequest> ReadRequestAsync(NamedPipeServerStream pipe, CancellationToken cancellationToken)
    {
        // Windows supplies the client's pipe token after its first bounded request has been read.
        // Deserialization is not authorization: dispatch still requires the authenticated caller SID.
        var request = await PipeFraming.ReadAsync<PipeRequest>(pipe, cancellationToken);
        string? sid = null;
        bool administrator = false;
        try { pipe.RunAsClient(() =>
        {
            using var identity = WindowsIdentity.GetCurrent();
            sid = identity.User?.Value;
            administrator = new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException)
        { throw new ClientFault("IPC_CALLER_INVALID"); }
        return new(sid ?? throw new ClientFault("IPC_CALLER_INVALID"), request, administrator);
    }

    public static NamedPipeServerStream Create(string name, bool firstInstance = true)
    {
        var acl = new PipeSecurity();
        acl.SetAccessRuleProtection(true, false);
        foreach (WellKnownSidType denied in new[] { WellKnownSidType.NetworkSid, WellKnownSidType.AnonymousSid })
            acl.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(denied, null),
                PipeAccessRights.FullControl, AccessControlType.Deny));
        acl.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.InteractiveSid, null),
            PipeAccessRights.ReadWrite | PipeAccessRights.Synchronize, AccessControlType.Allow));
        foreach (WellKnownSidType allowed in new[] { WellKnownSidType.LocalSystemSid, WellKnownSidType.BuiltinAdministratorsSid })
            acl.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(allowed, null),
                PipeAccessRights.FullControl, AccessControlType.Allow));
        return CreateWithSecurity(name, firstInstance, acl);
    }

    // The overload lets isolated OS fixtures give their own SID server rights without broadening the service ACL.
    internal static NamedPipeServerStream CreateWithSecurity(string name, bool firstInstance, PipeSecurity acl)
    {
        // Keep the first stream alive while its siblings serve status/off during a long connect request.
        var options = PipeOptions.Asynchronous | PipeOptions.WriteThrough;
        if (firstInstance) options |= PipeOptions.FirstPipeInstance;
        return NamedPipeServerStreamAcl.Create(name, PipeDirection.InOut, MaxInstances,
            PipeTransmissionMode.Byte, options,
            PipeContract.MaxBytes, PipeContract.MaxBytes, acl);
    }
}
