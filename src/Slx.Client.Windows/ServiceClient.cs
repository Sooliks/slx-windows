using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;
using Slx.Client.Core;

namespace Slx.Client.Windows;

public sealed class ServiceClient
{
    public async Task<PipeReply> SendAsync(PipeRequest request, CancellationToken cancellationToken = default)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(TimeSpan.FromSeconds(55));
        try
        {
            await using var pipe = new NamedPipeClientStream(".", PipeContract.Name, PipeDirection.InOut,
                PipeOptions.Asynchronous, TokenImpersonationLevel.Impersonation);
            await pipe.ConnectAsync(3000, budget.Token);
            PipeServiceIdentity.Validate(pipe.SafePipeHandle);
            await PipeFraming.WriteAsync(pipe, request, budget.Token);
            return await PipeFraming.ReadAsync<PipeReply>(pipe, budget.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new ClientFault("SERVICE_TIMEOUT"); }
        catch (TimeoutException) { throw new ClientFault("SERVICE_UNAVAILABLE"); }
        catch (IOException) { throw new ClientFault("SERVICE_UNAVAILABLE"); }
        catch (UnauthorizedAccessException) { throw new ClientFault("SERVICE_ACCESS_DENIED"); }
    }
}

internal static class PipeServiceIdentity
{
    public static void Validate(SafePipeHandle pipe)
    {
        if (!GetNamedPipeServerProcessId(pipe, out uint pipePid)) throw new ClientFault("SERVICE_IDENTITY_FAILED");
        IntPtr manager = OpenSCManagerW(null, null, 1);
        if (manager == IntPtr.Zero) throw new ClientFault("SERVICE_IDENTITY_FAILED");
        try
        {
            IntPtr service = OpenServiceW(manager, "SLXVPN", 4);
            if (service == IntPtr.Zero) throw new ClientFault("SERVICE_IDENTITY_FAILED");
            try
            {
                if (!QueryServiceStatusEx(service, 0, out var status,
                    (uint)Marshal.SizeOf<ServiceStatus>(), out _) || status.CurrentState != 4
                    || status.ProcessId == 0 || status.ProcessId != pipePid)
                    throw new ClientFault("SERVICE_IDENTITY_FAILED");
            }
            finally { CloseServiceHandle(service); }
        }
        finally { CloseServiceHandle(manager); }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ServiceStatus
    {
        public uint ServiceType, CurrentState, ControlsAccepted, Win32ExitCode, ServiceSpecificExitCode;
        public uint CheckPoint, WaitHint, ProcessId, ServiceFlags;
    }
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeServerProcessId(SafePipeHandle pipe, out uint serverProcessId);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr OpenSCManagerW(string? machine, string? database, uint access);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr OpenServiceW(IntPtr manager, string name, uint access);
    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryServiceStatusEx(IntPtr service, int level, out ServiceStatus status, uint size, out uint bytes);
    [DllImport("advapi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseServiceHandle(IntPtr handle);
}
