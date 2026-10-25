using System.Buffers.Binary;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
namespace Slx.Client.Tests;
/// <summary>One disposable junction between two fixture-owned local directories; no elevation or target mutation.</summary>
internal sealed class LocalJunctionFixture(string link) : IDisposable
{
    public static LocalJunctionFixture Create(string link, string target)
    {
        Directory.CreateDirectory(link);
        var fixture = new LocalJunctionFixture(link);
        try
        {
            using var handle = CreateFileW(link, 0x40000000, 7, 0, 3, 0x02200000, 0);
            if (handle.IsInvalid) throw new Win32Exception(Marshal.GetLastPInvokeError());
            string substitute = @"\??\" + Path.GetFullPath(target);
            byte[] sub = Encoding.Unicode.GetBytes(substitute), print = Encoding.Unicode.GetBytes(Path.GetFullPath(target));
            byte[] buffer = new byte[16 + sub.Length + 2 + print.Length + 2];
            BinaryPrimitives.WriteUInt32LittleEndian(buffer, 0xA0000003);
            BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(4), checked((ushort)(buffer.Length - 8)));
            BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(10), checked((ushort)sub.Length));
            BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(12), checked((ushort)(sub.Length + 2)));
            BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(14), checked((ushort)print.Length));
            sub.CopyTo(buffer, 16); print.CopyTo(buffer, 16 + sub.Length + 2);
            if (!DeviceIoControl(handle, 0x000900A4, buffer, (uint)buffer.Length, 0, 0, out _, 0))
                throw new Win32Exception(Marshal.GetLastPInvokeError());
            return fixture;
        }
        catch { fixture.Dispose(); throw; }
    }
    public void Dispose() { if (Directory.Exists(link)) Directory.Delete(link); }
    [DllImport("kernel32.dll", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(string path, uint access, uint share, nint security,
        uint disposition, uint flags, nint template);
    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(SafeFileHandle handle, uint code, byte[] input, uint length,
        nint output, uint outputLength, out uint returned, nint overlapped);
}
