using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Slx.Client.Desktop.Presentation;
using Slx.Client.Desktop.ViewModels;
namespace Slx.Client.Desktop;
public partial class MainWindow : Window
{
    private readonly MainViewModel model;
    private readonly bool renderOnly;
    private TrayIcon? tray;
    private bool exiting;
    public MainWindow(MainViewModel model, bool renderOnly = false)
    {
        this.model = model; this.renderOnly = renderOnly;
        InitializeComponent(); DataContext = model;
        SourceInitialized += (_, _) => { if (!renderOnly) tray = new TrayIcon(this, model); };
        Closing += ClosingWindow;
        Closed += (_, _) => { tray?.Dispose(); model.Dispose(); };
        model.PropertyChanged += ModelChanged;
        PreviewKeyDown += (_, args) =>
        {
            if (args.Key == Key.Escape && model.IsOverlayVisible)
            { model.CancelOverlayCommand.Execute(null); args.Handled = true; }
        };
    }
    private void ModelChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(MainViewModel.ImportVisible) or nameof(MainViewModel.DiscoveryVisible) or nameof(MainViewModel.RemoveVisible))
        {
            AppContent.IsEnabled = !model.IsOverlayVisible;
            Dispatcher.InvokeAsync(() =>
            {
                if (model.ImportVisible) ImportPanel.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
                else if (model.IsOverlayVisible) MoveFocus(new TraversalRequest(FocusNavigationDirection.Last));
                else AppContent.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
            }, DispatcherPriority.Input);
        }
    }
    private void ClosingWindow(object? sender, CancelEventArgs args)
    {
        if (exiting || renderOnly) return;
        args.Cancel = true;
        if (model.MinimizeToTray)
        {
            if (tray?.IsAvailable == true) Hide();
            else WindowState = WindowState.Minimized;
        }
        else _ = ExitAsync();
    }
    public void OpenToTray()
    {
        if (tray?.IsAvailable == true) Hide(); else WindowState = WindowState.Minimized;
    }
    public void Restore()
    {
        Show(); WindowState = WindowState.Normal; Activate();
    }
    public async Task ExitAsync()
    {
        if (exiting) return;
        exiting = true;
        if (!await model.ExitAsync())
        {
            Restore();
            if (new ExitUnknownDialog(this).ShowDialog() == true) model.ExitUiOnly();
            else exiting = false;
        }
    }
    private void Minimize_Click(object sender, RoutedEventArgs args) => WindowState = WindowState.Minimized;
    private void Maximize_Click(object sender, RoutedEventArgs args) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void Close_Click(object sender, RoutedEventArgs args) => Close();
}
