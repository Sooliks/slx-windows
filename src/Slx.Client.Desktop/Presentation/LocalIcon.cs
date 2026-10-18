using System.Runtime.InteropServices;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace Slx.Client.Desktop.Presentation;
internal static class LocalIcon
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
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern nint SHGetFileInfo(string path, uint attributes, out FileInfo info, uint size, uint flags);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(nint icon);
    public static ImageSource? Load(string path)
    {
        if (!Path.IsPathFullyQualified(path) || path.StartsWith(@"\\", StringComparison.Ordinal) || !string.Equals(Path.GetExtension(path), ".exe", StringComparison.OrdinalIgnoreCase)) return null;
        nint icon = 0;
        try
        {
            SHGetFileInfo(path, 0, out var info, (uint)Marshal.SizeOf<FileInfo>(), 0x100 | 0x1);
            icon = info.Icon;
            if (icon == 0) return null;
            var image = Imaging.CreateBitmapSourceFromHIcon(icon, Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(32, 32));
            image.Freeze(); return image;
        }
        catch (Exception error) when (error is ArgumentException or System.ComponentModel.Win32Exception) { return null; }
        finally { if (icon != 0) DestroyIcon(icon); }
    }
}
