using System.Diagnostics;
using Slx.Client.Core;

namespace Slx.Client.Service;

/// <summary>Starts the verified Xray binary with configuration only on stdin and a minimal environment.</summary>
internal static class XrayProcess
{
    /// <param name="track">Records the child before any later step can fail, so the caller can always stop it.</param>
    internal static Process Start(string executable, Action<Process> track)
    {
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(executable)!
        };
        start.ArgumentList.Add("run");
        start.ArgumentList.Add("-config");
        start.ArgumentList.Add("stdin:");
        start.ArgumentList.Add("-format");
        start.ArgumentList.Add("json");
        start.Environment.Clear();
        start.Environment["PATH"] = Environment.SystemDirectory;
        start.Environment["SYSTEMROOT"] = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        start.Environment["NODE_ENV"] = "production";
        var process = new Process { StartInfo = start };
        if (!process.Start()) { process.Dispose(); throw new ClientFault("CORE_START_FAILED"); }
        track(process);
        _ = DrainAsync(process.StandardOutput.BaseStream);
        _ = DrainAsync(process.StandardError.BaseStream);
        return process;
    }

    internal static async Task SendConfigAsync(Process process, string config, CancellationToken cancellationToken)
    {
        await process.StandardInput.WriteAsync(config.AsMemory(), cancellationToken);
        process.StandardInput.Close();
    }

    private static async Task DrainAsync(Stream stream)
    {
        try { await stream.CopyToAsync(Stream.Null); }
        catch (Exception error) when (error is IOException or ObjectDisposedException) { }
    }
}
