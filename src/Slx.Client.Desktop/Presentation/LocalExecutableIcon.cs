using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace Slx.Client.Desktop.Presentation;
internal static class LocalExecutableIcon
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct FileInfo
    {
        public nint Icon;
        public int Index;
        public uint Attributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string DisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string TypeName;
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "ExtractIconExW", ExactSpelling = true)]
    private static extern uint ExtractIconEx(string path, int index, out nint large, nint small, uint count);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "SHGetFileInfoW", ExactSpelling = true)]
    private static extern nint SHGetFileInfo(string path, uint attributes, out FileInfo info, uint size, uint flags);
    [DllImport("ole32.dll", ExactSpelling = true)]
    private static extern int CoInitializeEx(nint reserved, uint flags);
    [DllImport("ole32.dll", ExactSpelling = true)]
    private static extern void CoUninitialize();
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(nint icon);
    internal static ImageSource? Extract(string path)
    {
        if (!File.Exists(path)) return null;
        nint icon = 0;
        try
        {
            // Read the executable's own resource; no executable or icon handler is launched.
            var count = ExtractIconEx(path, 0, out icon, 0, 1);
            if (count is > 0 and < uint.MaxValue && icon != 0) return Bitmap(icon);
        }
        catch (Exception error) when (error is ArgumentException or System.ComponentModel.Win32Exception) { }
        finally { if (icon != 0) DestroyIcon(icon); }
        return Shell(path);
    }
    private static ImageSource? Shell(string path)
    {
        // Shell requires COM on this background worker. An existing STA is already initialized.
        var initialized = CoInitializeEx(0, 4); // COINIT_MULTITHREADED | COINIT_DISABLE_OLE1DDE
        if (initialized < 0 && initialized != unchecked((int)0x80010106)) return null;
        nint icon = 0;
        try
        {
            var result = SHGetFileInfo(path, 0, out var info, (uint)Marshal.SizeOf<FileInfo>(), 0x100);
            icon = info.Icon;
            return result != 0 && icon != 0 ? Bitmap(icon) : null;
        }
        catch (Exception error) when (error is ArgumentException or System.ComponentModel.Win32Exception) { return null; }
        finally
        {
            if (icon != 0) DestroyIcon(icon);
            if (initialized >= 0) CoUninitialize();
        }
    }
    private static ImageSource Bitmap(nint icon)
    {
        var image = Imaging.CreateBitmapSourceFromHIcon(icon, Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(32, 32));
        image.Freeze(); return image;
    }
}
