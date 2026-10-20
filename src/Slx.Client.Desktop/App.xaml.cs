using System.Windows;
using Slx.Client.Desktop.Presentation;
using Slx.Client.Desktop.ViewModels;
namespace Slx.Client.Desktop;
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var model = new MainViewModel(!e.Args.Contains("--render-check", StringComparer.Ordinal));
        var window = new MainWindow(model, e.Args.Contains("--render-check", StringComparer.Ordinal));
        MainWindow = window;
        if (e.Args.Contains("--render-check", StringComparer.Ordinal))
        {
            window.Show();
            _ = RenderCheck.CaptureAsync(window);
            return;
        }
        window.Show();
        model.Initialize(e.Args.Contains("--tray", StringComparer.Ordinal));
        if (e.Args.Contains("--tray", StringComparer.Ordinal)) window.OpenToTray();
    }
}
