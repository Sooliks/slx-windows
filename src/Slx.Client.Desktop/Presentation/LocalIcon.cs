using System.IO;
using System.Windows;
using System.Windows.Media;
namespace Slx.Client.Desktop.Presentation;
internal static class LocalIcon
{
    internal static ImageSource Fallback { get; } = CreateFallback();
    private static readonly LocalIconCache Cache = new(Fallback, LocalExecutableIcon.Extract);
    public static ImageSource Cached(string path) => IsExecutable(path) && Cache.TryCached(path, out var image) ? image : Fallback;
    public static ImageSource Cached(IEnumerable<string> paths) => paths.Take(64).Select(Cached)
        .FirstOrDefault(image => !ReferenceEquals(image, Fallback)) ?? Fallback;
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
}
