using Microsoft.Win32;
using Slx.Client.Core;

namespace Slx.Client.Windows;

public static class StartupRegistration
{
    private const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(Key);
        return key?.GetValue("SLX VPN") is string;
    }
    public static void SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(Key, true);
            if (enabled)
            {
                var path = Environment.ProcessPath ?? throw new ClientFault("STARTUP_UNAVAILABLE");
                key.SetValue("SLX VPN", "\"" + path + "\" --tray", RegistryValueKind.String);
            }
            else key.DeleteValue("SLX VPN", false);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        { throw new ClientFault("STARTUP_UNAVAILABLE"); }
    }
}
