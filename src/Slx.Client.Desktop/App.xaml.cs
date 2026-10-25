using System.Windows;
using Slx.Client.Desktop.Presentation;
using Slx.Client.Desktop.ViewModels;
namespace Slx.Client.Desktop;
public partial class App : Application
{
    private UiSingleInstance? uiInstance;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        bool render = e.Args.Contains("--render-check", StringComparer.Ordinal);
        bool tray = e.Args.Contains("--tray", StringComparer.Ordinal);
        if (!render && !UiSingleInstance.TryAcquire(!tray, out uiInstance)) { Shutdown(); return; }
        var model = new MainViewModel(!render);
        var window = new MainWindow(model, render);
        MainWindow = window;
        if (render)
        {
            window.Show();
            _ = RenderCheck.CaptureAsync(window);
            return;
        }
        window.Show();
        model.Initialize(tray);
        if (tray) window.OpenToTray();
        uiInstance!.StartListening(Dispatcher, window.Restore);
    }
    protected override void OnExit(ExitEventArgs e)
    {
        uiInstance?.Dispose(); uiInstance = null;
        base.OnExit(e);
    }
}
