using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using Slx.Client.Core;
namespace Slx.Client.Windows;
/// <summary>Read-only local handle walk. Never follows a reparse point or reads/executes file contents.</summary>
internal sealed unsafe class ExactExecutablePath : IDisposable
{
    internal const string Unsupported = "TRAFFIC_APPLICATION_PATH_UNSUPPORTED";
    private const uint ObjectCaseInsensitive = 0x40, ObjectDontReparse = 0x1000;
    private const uint ReadAttributes = 0x80, Synchronize = 0x100000, ShareReadWrite = 3, OpenExisting = 1;
    private const uint DirectoryOnly = 1, NonDirectoryOnly = 0x40, Synchronous = 0x20;
    private readonly List<SafeFileHandle> handles = [];
    internal ExactExecutablePath(string path) => Path = path; // Empty handles are only used by injected contract fixtures.
    public string Path { get; private set; }
    public static ExactExecutablePath Open(string input, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (!PlanValidator.IsExecutablePath(input)) throw new ClientFault(Unsupported);
        string full = System.IO.Path.GetFullPath(input);
        string root = System.IO.Path.GetPathRoot(full)!;
        var drive = new DriveInfo(root);
        if (drive.DriveType is DriveType.Network or DriveType.Unknown or DriveType.NoRootDirectory)
            throw new ClientFault(Unsupported);
        string[] components = full[root.Length..].Split(System.IO.Path.DirectorySeparatorChar);
        if (components.Length is 0 or >128 || components.Any(component => component.Length == 0))
            throw new ClientFault(Unsupported);
        var result = new ExactExecutablePath(full);
        try
        {
            result.handles.Add(OpenComponent(@"\??\" + root, null, true));
            for (int index = 0; index < components.Length; index++)
            {
                token.ThrowIfCancellationRequested();
                result.handles.Add(OpenComponent(components[index], result.handles[^1], index < components.Length - 1));
            }
            var buffer = new StringBuilder(32768);
            uint count = GetFinalPathNameByHandleW(result.handles[^1], buffer, (uint)buffer.Capacity, 0);
            if (count == 0) TrafficProtectionError.Application((uint)Marshal.GetLastPInvokeError(), true);
            if (count == 0 || count >= buffer.Capacity) throw new ClientFault(Unsupported);
            string normalized = buffer.ToString();
            if (normalized.StartsWith(@"\\?\", StringComparison.Ordinal)) normalized = normalized[4..];
            // This change repairs casing only. Short names, devices and remapped aliases remain unsupported.
            if (!PlanValidator.IsExecutablePath(normalized) || !full.Equals(normalized, StringComparison.OrdinalIgnoreCase))
                throw new ClientFault(Unsupported);
            token.ThrowIfCancellationRequested();
            result.Path = normalized;
            return result;
        }
        catch { result.Dispose(); throw; }
    }
    private static SafeFileHandle OpenComponent(string name, SafeFileHandle? parent, bool directory)
    {
        fixed (char* characters = name)
        {
            var text = new UnicodeString { Length = checked((ushort)(name.Length * 2)),
                MaximumLength = checked((ushort)((name.Length + 1) * 2)), Buffer = characters };
            var attributes = new ObjectAttributes { Length = (uint)sizeof(ObjectAttributes),
                RootDirectory = parent?.DangerousGetHandle() ?? 0, ObjectName = &text,
                Attributes = ObjectCaseInsensitive | ObjectDontReparse };
            int status = NtCreateFile(out var file, ReadAttributes | Synchronize, in attributes, out _, 0, 0,
                ShareReadWrite, OpenExisting, Synchronous | (directory ? DirectoryOnly : NonDirectoryOnly), 0, 0);
            if (status == 0 && !file.IsInvalid) return file;
            file.Dispose();
            if (status is unchecked((int)0xC000050B) or unchecked((int)0x8000002D)) throw new ClientFault(Unsupported);
            TrafficProtectionError.Application(RtlNtStatusToDosError(status), true);
            throw new ClientFault(Unsupported);
        }
    }
    public void Dispose()
    {
        for (int index = handles.Count - 1; index >= 0; index--) handles[index].Dispose();
        handles.Clear();
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct UnicodeString { public ushort Length, MaximumLength; public char* Buffer; }
    [StructLayout(LayoutKind.Sequential)]
    private struct ObjectAttributes
    {
        public uint Length; public nint RootDirectory; public UnicodeString* ObjectName;
        public uint Attributes; public nint SecurityDescriptor, SecurityQualityOfService;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct IoStatusBlock { public nint Status; public nuint Information; }
    [DllImport("ntdll.dll", ExactSpelling = true)]
    private static extern int NtCreateFile(out SafeFileHandle file, uint access, in ObjectAttributes attributes,
        out IoStatusBlock io, nint allocationSize, uint fileAttributes, uint share, uint disposition,
        uint options, nint eaBuffer, uint eaLength);
    [DllImport("ntdll.dll", ExactSpelling = true)]
    private static extern uint RtlNtStatusToDosError(int status);
    [DllImport("kernel32.dll", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandleW(SafeFileHandle file, StringBuilder path, uint size, uint flags);
}
