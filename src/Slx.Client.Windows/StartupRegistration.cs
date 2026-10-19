using Microsoft.Win32;
using Slx.Client.Core;
namespace Slx.Client.Windows;
public static class StartupRegistration
{
    private const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "SLX VPN";
    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(Key);
        return key?.GetValue(ValueName) is string value
            && string.Equals(value, Command(Environment.ProcessPath), StringComparison.OrdinalIgnoreCase);
    }
    public static void SetEnabled(bool enabled)
    {
        try
        {
            using var key = enabled ? Registry.CurrentUser.CreateSubKey(Key, true) : Registry.CurrentUser.OpenSubKey(Key, true);
            if (key is not null) Apply(key, enabled, Environment.ProcessPath);
        }
        catch (Exception error) when (error is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        { throw new ClientFault("STARTUP_UNAVAILABLE"); }
    }
    internal static void Apply(RegistryKey key, bool enabled, string? executable)
    {
        if (!enabled) { key.DeleteValue(ValueName, false); return; }
        string expected = Command(executable);
        if (!string.Equals(key.GetValue(ValueName) as string, expected, StringComparison.OrdinalIgnoreCase))
            key.SetValue(ValueName, expected, RegistryValueKind.String);
    }
    private static string Command(string? executable)
    {
        if (string.IsNullOrWhiteSpace(executable) || !Path.IsPathFullyQualified(executable)
            || executable.IndexOfAny(['"', '\r', '\n']) >= 0
            || !string.Equals(Path.GetFileName(executable), "SLX VPN.exe", StringComparison.OrdinalIgnoreCase))
            throw new ClientFault("STARTUP_UNAVAILABLE");
        return "\"" + Path.GetFullPath(executable) + "\" --tray";
    }
}
