using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Slx.Client.Core;

namespace Slx.Client.Service;

internal static class InteractiveSessions
{
    public static void RequireSingleOwner(string ownerSid)
    {
        if (!WTSEnumerateSessionsW(IntPtr.Zero, 0, 1, out var buffer, out int count))
            throw new ClientFault("SESSION_CHECK_FAILED");
        try
        {
            int size = Marshal.SizeOf<Session>();
            var users = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < count; index++)
            {
                var session = Marshal.PtrToStructure<Session>(IntPtr.Add(buffer, index * size));
                if (session.State is not (0 or 1 or 4)) continue;
                string user = Query(session.Id, 5);
                if (string.IsNullOrWhiteSpace(user)) continue;
                string domain = Query(session.Id, 7);
                try
                {
                    var account = new NTAccount(string.IsNullOrWhiteSpace(domain) ? user : domain + "\\" + user);
                    users.Add(((SecurityIdentifier)account.Translate(typeof(SecurityIdentifier))).Value);
                }
                catch (IdentityNotMappedException) { throw new ClientFault("SESSION_CHECK_FAILED"); }
            }
            if (users.Count != 1 || !users.Contains(ownerSid)) throw new ClientFault("MULTI_SESSION_UNSUPPORTED");
        }
        finally { WTSFreeMemory(buffer); }
    }

    private static string Query(int sessionId, int info)
    {
        if (!WTSQuerySessionInformationW(IntPtr.Zero, sessionId, info, out var pointer, out _))
            throw new ClientFault("SESSION_CHECK_FAILED");
        try { return Marshal.PtrToStringUni(pointer) ?? ""; }
        finally { WTSFreeMemory(pointer); }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Session { public int Id; public IntPtr Station; public int State; }
    [DllImport("Wtsapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WTSEnumerateSessionsW(IntPtr server, int reserved, int version, out IntPtr sessions, out int count);
    [DllImport("Wtsapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WTSQuerySessionInformationW(IntPtr server, int sessionId, int info, out IntPtr buffer, out int bytes);
    [DllImport("Wtsapi32.dll")]
    private static extern void WTSFreeMemory(IntPtr memory);
}
