using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace Slx.Client.UiTests;
internal static class UiScreenshots
{
    public static void Save(FrameworkElement element, string name, bool localOrigin = false)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "Slx.Windows.slnx"))) root = root.Parent;
        if (root == null) throw new InvalidOperationException("UI evidence root not found");
        var directory = Path.Combine(root.FullName, "artifacts", "ui"); Directory.CreateDirectory(directory);
        element.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth), (int)Math.Ceiling(element.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        if (localOrigin)
        {
            // Descendant RenderTargetBitmap retains its parent offset; a brush normalizes the capture to local bounds.
            var drawing = new DrawingVisual();
            using (var context = drawing.RenderOpen())
            {
                context.DrawRectangle(Window.GetWindow(element)?.Background, null, new Rect(element.RenderSize));
                context.DrawRectangle(new VisualBrush(element), null, new Rect(element.RenderSize));
            }
            bitmap.Render(drawing);
        }
        else bitmap.Render(element);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var output = File.Create(Path.Combine(directory, name + ".png")); encoder.Save(output);
    }
}
