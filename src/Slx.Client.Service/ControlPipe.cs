using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using Slx.Client.Core;
using Slx.Client.Windows;

namespace Slx.Client.Service;

internal static class ControlPipe
{
    internal sealed record CallerRequest(string Sid, PipeRequest Request);

    public static async Task<CallerRequest> ReadRequestAsync(NamedPipeServerStream pipe, CancellationToken cancellationToken)
    {
        // Windows supplies the client's pipe token after its first bounded request has been read.
        // Deserialization is not authorization: dispatch still requires the authenticated caller SID.
        var request = await PipeFraming.ReadAsync<PipeRequest>(pipe, cancellationToken);
        string? sid = null;
        try { pipe.RunAsClient(() => sid = WindowsIdentity.GetCurrent().User?.Value); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException)
        { throw new ClientFault("IPC_CALLER_INVALID"); }
        return new(sid ?? throw new ClientFault("IPC_CALLER_INVALID"), request);
    }

    public static NamedPipeServerStream Create(string name)
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
        return NamedPipeServerStreamAcl.Create(name, PipeDirection.InOut, 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.WriteThrough | PipeOptions.FirstPipeInstance,
            PipeContract.MaxBytes, PipeContract.MaxBytes, acl);
    }
}
