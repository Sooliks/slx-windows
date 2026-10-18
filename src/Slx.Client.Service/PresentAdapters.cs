using System.Runtime.InteropServices;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using Slx.Client.Core;
namespace Slx.Client.Service;
internal static class PresentAdapters
{
    // GetAdaptersAddresses also returns detached ghost records. Only present PnP
    // devices constitute a conflict; down/disabled but present devices still count.
    public static bool Contains(string id)
    {
        if (!Guid.TryParse(id, out var target)) throw new ClientFault("ADAPTER_QUERY_FAILED");
        var networkClass = new Guid("4d36e972-e325-11ce-bfc1-08002be10318");
        var devices = SetupDiGetClassDevsW(ref networkClass, null, 0, 2);
        if (devices == -1) throw new ClientFault("ADAPTER_QUERY_FAILED");
        try
        {
            for (uint index = 0; ; index++)
            {
                var device = new DeviceInfo { Size = (uint)Marshal.SizeOf<DeviceInfo>() };
                if (!SetupDiEnumDeviceInfo(devices, index, ref device))
                {
                    if (Marshal.GetLastWin32Error() == 259) return false;
                    throw new ClientFault("ADAPTER_QUERY_FAILED");
                }
                var key = SetupDiOpenDevRegKey(devices, ref device, 1, 0, 2, 1);
                if (key == -1)
                {
                    int error = Marshal.GetLastWin32Error();
                    if (error == 2 || unchecked((uint)error) == 0xe0000204) continue;
                    throw new ClientFault("ADAPTER_QUERY_FAILED");
                }
                using var handle = new SafeRegistryHandle(key, true);
                using var registry = RegistryKey.FromHandle(handle);
                if (registry.GetValue("NetCfgInstanceId") is string value
                    && Guid.TryParse(value, out var candidate) && candidate == target) return true;
            }
        }
        catch (Exception error) when (error is System.Security.SecurityException or UnauthorizedAccessException or System.IO.IOException)
        { throw new ClientFault("ADAPTER_QUERY_FAILED"); }
        finally { SetupDiDestroyDeviceInfoList(devices); }
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct DeviceInfo { public uint Size; public Guid ClassGuid; public uint DeviceInstance; public nuint Reserved; }
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint SetupDiGetClassDevsW(ref Guid classGuid, string? enumerator, nint parent, uint flags);
    [DllImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiEnumDeviceInfo(nint devices, uint index, ref DeviceInfo device);
    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern nint SetupDiOpenDevRegKey(nint devices, ref DeviceInfo device, uint scope, uint profile, uint type, uint access);
    [DllImport("setupapi.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiDestroyDeviceInfoList(nint devices);
}
