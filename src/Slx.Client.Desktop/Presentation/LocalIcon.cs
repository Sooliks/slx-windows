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
    internal static ImageSource Fallback { get; } = CreateFallback();
    private static readonly LocalIconCache Cache = new(Fallback, Extract);
    public static ImageSource Cached(string path) => IsExecutable(path) && Cache.TryCached(path, out var image) ? image : Fallback;
    public static ImageSource Load(string path)
    {
        if (!IsExecutable(path)) return Fallback;
        if (Cache.TryCached(path, out var image)) return image;
        _ = LoadAsync(path);
        return Fallback;
    }
    public static Task<ImageSource> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return IsExecutable(path) ? Cache.LoadAsync(path, cancellationToken) : Task.FromResult(Fallback);
    }
    private static bool IsExecutable(string path) => !string.IsNullOrWhiteSpace(path) && Path.IsPathFullyQualified(path)
        && !path.StartsWith(@"\\", StringComparison.Ordinal) && string.Equals(Path.GetExtension(path), ".exe", StringComparison.OrdinalIgnoreCase);
    private static ImageSource CreateFallback()
    {
        var drawing = new DrawingGroup();
        drawing.Children.Add(new GeometryDrawing(new SolidColorBrush(Color.FromRgb(115, 129, 150)), null, new RectangleGeometry(new Rect(2, 4, 28, 24), 4, 4)));
        drawing.Children.Add(new GeometryDrawing(Brushes.White, null, Geometry.Parse("M7,9 L25,9 25,12 7,12 Z M8,17 L13,17 13,23 8,23 Z M17,17 L23,17 23,19 17,19 Z M17,22 L23,22 23,24 17,24 Z")));
        var image = new DrawingImage(drawing); image.Freeze(); return image;
    }
    private static ImageSource? Extract(string path)
    {
        if (!IsExecutable(path)) return null;
        nint icon = 0;
        try
        {
            SHGetFileInfo(path, 0, out var info, (uint)Marshal.SizeOf<FileInfo>(), 0x100);
            icon = info.Icon;
            if (icon == 0) return null;
            var image = Imaging.CreateBitmapSourceFromHIcon(icon, Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(32, 32));
            image.Freeze(); return image;
        }
        catch (Exception error) when (error is ArgumentException or System.ComponentModel.Win32Exception) { return null; }
        finally { if (icon != 0) DestroyIcon(icon); }
    }
}
