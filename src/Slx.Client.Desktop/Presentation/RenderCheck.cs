using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Slx.Client.Desktop.ViewModels;
namespace Slx.Client.Desktop.Presentation;
internal static class RenderCheck
{
    public static async Task CaptureAsync(MainWindow window)
    {
        var directory = Path.Combine(Environment.CurrentDirectory, "artifacts", "ui");
        Directory.CreateDirectory(directory);
        var model = (MainViewModel)window.DataContext;
        var cases = new[]
        {
            (Name: "slx-desktop-empty", Page: "connection", Language: "ru", Theme: "olive", Width: 1200d, Height: 820d),
            (Name: "slx-apps-narrow", Page: "applications", Language: "ru", Theme: "olive", Width: 940d, Height: 660d),
            (Name: "slx-settings", Page: "settings", Language: "ru", Theme: "olive", Width: 1200d, Height: 820d),
            (Name: "slx-settings-narrow", Page: "settings", Language: "ru", Theme: "olive", Width: 940d, Height: 660d),
            (Name: "slx-help-terminal-en", Page: "help", Language: "en", Theme: "terminal", Width: 1200d, Height: 820d),
            (Name: "slx-import", Page: "connection", Language: "ru", Theme: "olive", Width: 940d, Height: 660d)
        };
        foreach (var item in cases)
        {
            window.Width = item.Width; window.Height = item.Height;
            model.Page = item.Page; model.Language = item.Language; model.Theme = item.Theme;
            model.ImportVisible = item.Name == "slx-import";
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            window.UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(window);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var file = File.Create(Path.Combine(directory, $"{item.Name}.png")); encoder.Save(file);
        }
        Application.Current.Shutdown();
    }
}
