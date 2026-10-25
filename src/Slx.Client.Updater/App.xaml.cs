using System.Windows;
using Slx.Client.Windows;

namespace Slx.Client.Updater;

public partial class App : Application
{
    private IntegratedUpdateGuard? guard;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Length != 2 || e.Args[0] != "--session" || !Guid.TryParseExact(e.Args[1], "N", out var id) || id == Guid.Empty)
        { Shutdown(1); return; }
        try
        {
            guard = IntegratedUpdateGuard.TryAcquire();
            if (guard is null) { Shutdown(); return; }
            var session = new IntegratedUpdateSession(id);
            var window = new UpdateWindow(session, ReleaseGuard); MainWindow = window;
            guard.Listen(() => Dispatcher.BeginInvoke(() => { window.Show(); window.WindowState = WindowState.Normal; window.Activate(); }));
            window.Show(); session.SignalReady();
        }
        catch (Exception error) when (error is Slx.Client.Core.ClientFault or System.IO.IOException or UnauthorizedAccessException
            or System.Security.Cryptography.CryptographicException or System.Text.Json.JsonException or ArgumentException)
        { ReleaseGuard(); Shutdown(1); }
    }
    private void ReleaseGuard() { guard?.Dispose(); guard = null; }
    protected override void OnExit(ExitEventArgs e) { ReleaseGuard(); base.OnExit(e); }
}
