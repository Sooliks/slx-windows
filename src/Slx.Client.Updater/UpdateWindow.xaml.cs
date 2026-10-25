using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using Slx.Client.Windows;
namespace Slx.Client.Updater;
public partial class UpdateWindow : Window
{
    private readonly IntegratedUpdateSession? session;
    private readonly Action releaseGuard = () => { };
    private readonly bool ru;
    private readonly string targetVersion;
    private bool busy = true;
    private int runGeneration;
    public UpdateWindow(IntegratedUpdateSession value, Action release) : this(value.TargetVersion, value.Language)
    {
        session = value; releaseGuard = release;
        Loaded += async (_, _) => await RunAsync(); Closing += OnClosing;
    }
    // Presentation-only fixture: no stored session, Loaded handler, service or installer can be constructed here.
    internal UpdateWindow(string targetVersion, string language)
    {
        this.targetVersion = targetVersion; ru = language == "ru"; InitializeComponent();
        RetryButton.Content = ru ? "Повторить" : "Try again"; ReturnButton.Content = ru ? "Открыть SLX" : "Open SLX";
        VersionCaption.Text = ru ? "ОБНОВЛЕНИЕ · " + targetVersion : "UPDATE · " + targetVersion;
        AutomationProperties.SetName(MinimizeCaption, ru ? "Свернуть" : "Minimize");
        AutomationProperties.SetName(CloseCaption, ru ? "Закрыть обновление" : "Close update");
        MinimizeCaption.ToolTip = AutomationProperties.GetName(MinimizeCaption); CloseCaption.ToolTip = AutomationProperties.GetName(CloseCaption);
        ApplyContrast(SystemParameters.HighContrast); SystemParameters.StaticPropertyChanged += SystemAppearanceChanged;
        Closed += (_, _) => SystemParameters.StaticPropertyChanged -= SystemAppearanceChanged;
        PresentPhase(IntegratedUpdatePhase.WaitingForApplication);
    }
    private async Task RunAsync()
    {
        if (session is null) return;
        var generation = ++runGeneration;
        busy = true; RetryButton.Visibility = ReturnButton.Visibility = Visibility.Collapsed; Activity.Visibility = Visibility.Visible;
        IntegratedUpdateResult result;
        try { result = await session.RunAsync(new Progress<IntegratedUpdatePhase>(phase => { if (busy && generation == runGeneration && !IsTerminal(phase)) PresentPhase(phase); })); }
        catch (Exception error) when (error is Slx.Client.Core.ClientFault or System.IO.IOException or UnauthorizedAccessException)
        { busy = false; PresentPhase(IntegratedUpdatePhase.Failed); return; }
        busy = false; PresentPhase(result.Phase);
        if (result.Phase == IntegratedUpdatePhase.Succeeded) OpenApplication();
    }
    private async void Retry(object sender, RoutedEventArgs e) { if (!busy && session is not null) await RunAsync(); }
    private void Return(object sender, RoutedEventArgs e) { if (!busy && session is not null) OpenApplication(); }
    private void OpenApplication()
    {
        if (session is null) return;
        releaseGuard();
        try { if (session.ReturnToApplication()) Application.Current.Shutdown(); else ShowManualOpen(); }
        catch (Exception error) when (error is Slx.Client.Core.ClientFault or System.IO.IOException or UnauthorizedAccessException
            or System.ComponentModel.Win32Exception)
        { ShowManualOpen(); }
    }
    private void ShowManualOpen() => Heading.Text = ru ? "Откройте SLX из меню «Пуск»" : "Open SLX from the Start menu";
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (busy) { e.Cancel = true; WindowState = WindowState.Minimized; }
        else { releaseGuard(); Application.Current.Shutdown(); }
    }
    private void Minimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void CloseWindow(object sender, RoutedEventArgs e) => Close();
}
