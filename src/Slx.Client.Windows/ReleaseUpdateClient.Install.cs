using System.ComponentModel;
using System.Diagnostics;
using Slx.Client.Core;

namespace Slx.Client.Windows;

public enum UpdateLaunchResult { Started, Cancelled }
public sealed partial class ReleaseUpdateClient
{
    /// <summary>Call only for an explicit user action after authoritative confirmation that VPN is disconnected.</summary>
    public async Task<UpdateLaunchResult> LaunchInstallerAsync(PreparedUpdate prepared, CancellationToken cancellationToken = default)
    {
        UpdateSourcePolicy.Validate(prepared.Offer);
        try
        {
            var path = Path.GetFullPath(prepared.InstallerPath);
            var directory = Path.GetDirectoryName(path) ?? throw new ClientFault("UPDATE_INVALID");
            if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || Path.GetDirectoryName(directory) != root || !Guid.TryParseExact(Path.GetFileName(directory), "N", out _)
                || Path.GetFileName(path) != prepared.Offer.AssetName) throw new ClientFault("UPDATE_INVALID");
            EnsureLocalDirectory(directory);
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous);
            await VerifyAsync(file, prepared.Offer, cancellationToken);
            using var zoneFile = new FileStream(path + ":Zone.Identifier", FileMode.Open, FileAccess.Read, FileShare.Read);
            if (zoneFile.Length is <= 0 or > 2048) throw new ClientFault("UPDATE_INTEGRITY_FAILED");
            using var zoneReader = new StreamReader(zoneFile);
            var zone = await zoneReader.ReadToEndAsync(cancellationToken);
            if (!zone.StartsWith("[ZoneTransfer]\r\nZoneId=3\r\n", StringComparison.Ordinal)) throw new ClientFault("UPDATE_INTEGRITY_FAILED");
            cancellationToken.ThrowIfCancellationRequested();
            return launch(new ProcessStartInfo(path) { UseShellExecute = true, Verb = "runas", WorkingDirectory = directory })
                ? UpdateLaunchResult.Started : throw new ClientFault("UPDATE_INSTALL_FAILED");
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == 1223) { return UpdateLaunchResult.Cancelled; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or Win32Exception or InvalidOperationException)
        { throw new ClientFault("UPDATE_INSTALL_FAILED"); }
    }
}
