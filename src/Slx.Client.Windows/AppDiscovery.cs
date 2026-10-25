using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Win32;
using Slx.Client.Core;

namespace Slx.Client.Windows;

public static class AppDiscovery
{
    public static AppRule FromExecutable(string raw)
    {
        var path = Path.GetFullPath(raw);
        if (!Path.IsPathFullyQualified(path) || path.StartsWith(@"\\", StringComparison.Ordinal)
            || !string.Equals(Path.GetExtension(path), ".exe", StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
            throw new ClientFault("INVALID_APPLICATION");
        string? label = null;
        try { label = FileVersionInfo.GetVersionInfo(path).FileDescription; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Win32Exception) { }
        var name = string.IsNullOrWhiteSpace(label) ? Path.GetFileNameWithoutExtension(path) : label.Trim();
        return new(Guid.NewGuid(), name.Length > 100 ? name[..100] : name, path);
    }
    public static IReadOnlyList<AppRule> RunningApplications(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var found = new Dictionary<string, AppRule>(StringComparer.OrdinalIgnoreCase);
        using var current = Process.GetCurrentProcess();
        var session = current.SessionId;
        var processes = Process.GetProcesses();
        try
        {
            foreach (var process in processes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    if (process.SessionId != session) continue;
                    if (process.Id == Environment.ProcessId || process.MainWindowHandle == IntPtr.Zero) continue;
                    var path = process.MainModule?.FileName;
                    if (path is not null && LibraryApplication.IsCandidate(path) && !path.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.Windows) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && !found.ContainsKey(path)) found[path] = FromExecutable(path);
                }
                catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException or UnauthorizedAccessException or ClientFault) { }
            }
        }
        finally { foreach (var process in processes) process.Dispose(); }
        return found.Values.OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase).Take(300).ToArray();
    }
    public static IReadOnlyList<AppRule> InstalledApplications(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var found = new Dictionary<string, AppRule>(StringComparer.OrdinalIgnoreCase);
        foreach (var hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
            foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var registry = RegistryKey.OpenBaseKey(hive, view);
                using var entries = registry.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
                if (entries is null) continue;
                foreach (var name in entries.GetSubKeyNames().Take(1000))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    using var entry = entries.OpenSubKey(name);
                    if (entry?.GetValue("DisplayIcon") is not string icon) continue;
                    var path = DisplayIconPath.Parse(icon);
                    if (!LibraryApplication.IsCandidate(path)) continue;
                    try { var app = FromExecutable(path); found.TryAdd(app.ExecutablePath, app); }
                    catch (Exception ex) when (ex is ClientFault or IOException or UnauthorizedAccessException or ArgumentException) { }
                }
            }
        return found.Values.OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase).Take(300).ToArray();
    }
}
