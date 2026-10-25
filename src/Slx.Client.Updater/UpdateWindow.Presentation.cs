using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Media;
using Slx.Client.Windows;
namespace Slx.Client.Updater;
public partial class UpdateWindow
{
    internal void PresentPhase(IntegratedUpdatePhase phase)
    {
        var terminal = IsTerminal(phase);
        Activity.Visibility = terminal ? Visibility.Collapsed : Visibility.Visible;
        RetryButton.Visibility = phase is IntegratedUpdatePhase.Failed or IntegratedUpdatePhase.Cancelled ? Visibility.Visible : Visibility.Collapsed;
        ReturnButton.Visibility = terminal ? Visibility.Visible : Visibility.Collapsed;
        busy = !terminal;
        AutomationProperties.SetName(CloseCaption, terminal ? ru ? "Закрыть обновление" : "Close update"
            : ru ? "Скрыть окно обновления" : "Hide update window");
        CloseCaption.ToolTip = terminal ? AutomationProperties.GetName(CloseCaption)
            : ru ? "Обновление продолжится в фоне" : "Updating continues in the background";
        Heading.Text = (ru, phase) switch
        {
            (true, IntegratedUpdatePhase.WaitingForApplication) => "Готовим SLX к обновлению",
            (true, IntegratedUpdatePhase.AwaitingPermission) => "Разрешите обновление SLX",
            (true, IntegratedUpdatePhase.Installing) => "Устанавливаем обновление",
            (true, IntegratedUpdatePhase.Verifying) => "Проверяем новую версию",
            (true, IntegratedUpdatePhase.Succeeded) => "SLX обновлён",
            (true, IntegratedUpdatePhase.Cancelled) => "Обновление отложено",
            (true, IntegratedUpdatePhase.Failed) => "Не удалось завершить обновление",
            (true, IntegratedUpdatePhase.RestartRequired) => "Нужен перезапуск Windows",
            (true, IntegratedUpdatePhase.Unknown) => "Обновление не подтверждено",
            (true, _) => "Проверяем обновление",
            (false, IntegratedUpdatePhase.WaitingForApplication) => "Preparing SLX to update",
            (false, IntegratedUpdatePhase.AwaitingPermission) => "Allow the SLX update",
            (false, IntegratedUpdatePhase.Installing) => "Installing the update",
            (false, IntegratedUpdatePhase.Verifying) => "Checking the new version",
            (false, IntegratedUpdatePhase.Succeeded) => "SLX has been updated",
            (false, IntegratedUpdatePhase.Cancelled) => "Update postponed",
            (false, IntegratedUpdatePhase.Failed) => "The update could not finish",
            (false, IntegratedUpdatePhase.RestartRequired) => "Restart Windows to finish",
            (false, IntegratedUpdatePhase.Unknown) => "Update could not be confirmed",
            _ => "Checking the update"
        };
        Description.Text = (ru, phase) switch
        {
            (true, IntegratedUpdatePhase.WaitingForApplication) => "Ждём завершения предыдущего окна SLX. Настройки и подписка останутся на месте.",
            (true, IntegratedUpdatePhase.AwaitingPermission) => "Подтвердите разрешение в окне Windows. Мастер установки открывать не нужно.",
            (true, IntegratedUpdatePhase.Cancelled) => "Разрешение администратора отменено. Можно повторить попытку или вернуться в SLX.",
            (true, IntegratedUpdatePhase.Failed) => "Обновление сохранено. Можно повторить попытку. Если служба недоступна, обратитесь в поддержку через раздел «Помощь» в SLX.",
            (true, IntegratedUpdatePhase.RestartRequired) => "Сохраните работу и перезагрузите компьютер, когда будет удобно. VPN пока не подключится автоматически.",
            (true, IntegratedUpdatePhase.Succeeded) => "Приложение и служба проверены. Сейчас откроется обновлённый SLX с вашими настройками.",
            (true, _) => "Настройки и подписка сохранятся. SLX откроется после проверки приложения и службы.",
            (false, IntegratedUpdatePhase.WaitingForApplication) => "Waiting for the previous SLX window to close. Your settings and subscription are kept.",
            (false, IntegratedUpdatePhase.AwaitingPermission) => "Approve the Windows permission prompt. There is no setup wizard to complete.",
            (false, IntegratedUpdatePhase.Cancelled) => "Administrator permission was cancelled. Try again or return to SLX.",
            (false, IntegratedUpdatePhase.Failed) => "The update is saved. You can try again. If the service is unavailable, contact support through SLX Help.",
            (false, IntegratedUpdatePhase.RestartRequired) => "Save your work and restart Windows when convenient. VPN will not reconnect automatically yet.",
            (false, IntegratedUpdatePhase.Succeeded) => "The application and service have been checked. Updated SLX will open with your settings.",
            _ => "Your settings and subscription are kept. SLX will open after the application and service are checked."
        };
        Detail.Text = (ru, phase) switch
        {
            (true, IntegratedUpdatePhase.AwaitingPermission) => "Окно Windows может находиться поверх других приложений.",
            (true, IntegratedUpdatePhase.Failed or IntegratedUpdatePhase.Unknown) => "Не удалось подтвердить завершение. Состояние VPN нужно проверить в SLX.",
            (true, IntegratedUpdatePhase.RestartRequired) => "Перезапуск компьютера остаётся под вашим контролем.",
            (true, IntegratedUpdatePhase.Cancelled) => "Повторное разрешение Windows запросим только по нажатию «Повторить».",
            (true, IntegratedUpdatePhase.Succeeded) => "Возвращаем вас в приложение…",
            (true, _) => "Не выключайте компьютер во время установки.",
            (false, IntegratedUpdatePhase.AwaitingPermission) => "The Windows prompt may appear above other applications.",
            (false, IntegratedUpdatePhase.Failed or IntegratedUpdatePhase.Unknown) => "Completion is unconfirmed. Check the VPN state in SLX.",
            (false, IntegratedUpdatePhase.RestartRequired) => "You decide when to restart your computer.",
            (false, IntegratedUpdatePhase.Cancelled) => "Windows permission is requested again only when you choose Try again.",
            (false, IntegratedUpdatePhase.Succeeded) => "Returning to the application…",
            _ => "Keep your computer on during installation."
        };
        AutomationProperties.SetName(Activity, Heading.Text);
    }
    private static bool IsTerminal(IntegratedUpdatePhase phase) => phase is IntegratedUpdatePhase.Succeeded or IntegratedUpdatePhase.Cancelled
        or IntegratedUpdatePhase.Failed or IntegratedUpdatePhase.RestartRequired or IntegratedUpdatePhase.Unknown;
    private void SystemAppearanceChanged(object? sender, PropertyChangedEventArgs e)
    { if (e.PropertyName == nameof(SystemParameters.HighContrast)) Dispatcher.Invoke(() => ApplyContrast(SystemParameters.HighContrast)); }
    internal void ApplyContrast(bool highContrast)
    {
        foreach (var (key, color) in new[] { ("UpdateCanvas", "#10120E"), ("UpdateSurface", "#191D16"), ("UpdateSurfaceHover", "#252C1E"),
            ("UpdateBorder", "#343B2B"), ("UpdateText", "#F1F3E9"), ("UpdateMuted", "#B9BFAF"), ("UpdateAccent", "#DFEF4B"),
            ("UpdateAccentHover", "#EDF77A"), ("UpdateAccentText", "#10120E"), ("UpdateDanger", "#762F32") })
        {
            Brush brush = highContrast ? key switch
            {
                "UpdateCanvas" or "UpdateSurface" or "UpdateSurfaceHover" or "UpdateDanger" => SystemColors.WindowBrush,
                "UpdateText" or "UpdateMuted" or "UpdateBorder" => SystemColors.WindowTextBrush,
                "UpdateAccentText" => SystemColors.HighlightTextBrush, _ => SystemColors.HighlightBrush
            } : new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
            Resources[key] = brush;
        }
    }
}
