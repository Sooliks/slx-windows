using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Slx.Client.Windows;

/// <summary>Reads the documented OS last-start observation only for a known restart-required result.</summary>
internal static class IntegratedUpdateBoot
{
    private const string Command = @"$ErrorActionPreference='Stop'; Import-Module -Name ($PSHOME+'\Modules\CimCmdlets\CimCmdlets.psd1') -ErrorAction Stop; "
        + @"$boot=(CimCmdlets\Get-CimInstance -ClassName Win32_OperatingSystem -Property LastBootUpTime -OperationTimeoutSec 3 -ErrorAction Stop).LastBootUpTime; "
        + "[Console]::Out.Write($boot.ToUniversalTime().ToString('O',[Globalization.CultureInfo]::InvariantCulture))";
    internal static async Task<DateTimeOffset?> ReadAsync(CancellationToken cancellationToken = default)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(TimeSpan.FromSeconds(8));
        var directory = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0");
        var start = new ProcessStartInfo(Path.Combine(directory, "powershell.exe"))
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
            WorkingDirectory = directory };
        foreach (var argument in new[] { "-NoProfile", "-NonInteractive", "-Command", Command }) start.ArgumentList.Add(argument);
        start.Environment.Clear();
        start.Environment["SystemRoot"] = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        start.Environment["windir"] = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        start.Environment["PATH"] = Environment.SystemDirectory;
        start.Environment["PSModulePath"] = Path.Combine(directory, "Modules");
        using var process = new Process { StartInfo = start };
        try
        {
            budget.Token.ThrowIfCancellationRequested();
            if (!process.Start()) return null;
            var output = ReadBoundedAsync(process.StandardOutput, budget.Token);
            var errors = ReadBoundedAsync(process.StandardError, budget.Token);
            await Task.WhenAll(output, errors, process.WaitForExitAsync(budget.Token)).WaitAsync(budget.Token);
            if (process.ExitCode != 0 || (await errors).Length != 0) return null;
            return Parse(await output);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or Win32Exception
            or InvalidOperationException or OperationCanceledException) { return null; }
        finally
        {
            try { if (process.Id != 0 && !process.HasExited) process.Kill(); }
            catch (Exception error) when (error is InvalidOperationException or Win32Exception or NotSupportedException) { }
        }
    }
    private static async Task<string> ReadBoundedAsync(StreamReader reader, CancellationToken token)
    {
        var value = new StringBuilder();
        var buffer = new char[128];
        int read;
        while ((read = await reader.ReadAsync(buffer, token)) != 0)
        {
            if (value.Length + read > 256) throw new InvalidDataException();
            value.Append(buffer, 0, read);
        }
        return value.ToString();
    }
    internal static DateTimeOffset? Parse(string value) => value.Length <= 256
        && DateTimeOffset.TryParseExact(value.Trim(), "O", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
        && parsed.Offset == TimeSpan.Zero && parsed > DateTimeOffset.UnixEpoch ? parsed : null;
    internal static bool Different(DateTimeOffset? previous, DateTimeOffset? current) => previous is { } before
        && current is { } after && before.Offset == TimeSpan.Zero && after.Offset == TimeSpan.Zero
        && before > DateTimeOffset.UnixEpoch && after > DateTimeOffset.UnixEpoch
        && Math.Abs((after - before).TotalSeconds) > 1;
}
