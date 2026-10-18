using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
namespace Slx.Client.UiTests;
internal static class Sta
{
    public static async Task Run(Action test)
    {
        var finished = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            Exception? failure = null;
            try
            {
                var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                foreach (var theme in new[] { "Olive", "Controls" })
                    app.Resources.MergedDictionaries.Add(new ResourceDictionary
                    { Source = new Uri($"/SLX VPN;component/Themes/{theme}.xaml", UriKind.Relative) });
                app.DispatcherUnhandledException += (_, args) =>
                {
                    failure ??= args.Exception; args.Handled = true; app.Shutdown(1);
                };
                app.Dispatcher.InvokeAsync(() =>
                {
                    try { test(); }
                    catch (Exception error) { failure = error; }
                    finally
                    {
                        foreach (Window window in app.Windows.Cast<Window>().ToArray()) window.Close();
                        app.Dispatcher.InvokeAsync(() => app.Shutdown(), DispatcherPriority.SystemIdle);
                    }
                });
                app.Run();
            }
            catch (Exception error) { failure ??= error; }
            finally { finished.TrySetResult(failure); }
        }) { Name = "SLX UI regression dispatcher" };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        var failure = await finished.Task.WaitAsync(TimeSpan.FromSeconds(45));
        if (!thread.Join(TimeSpan.FromSeconds(5))) throw new TimeoutException("UI dispatcher did not exit");
        if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
    public static void Pump() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.SystemIdle);
    public static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match) yield return match;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }
}
