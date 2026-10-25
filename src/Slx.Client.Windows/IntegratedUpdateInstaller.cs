using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;
using Slx.Client.Core;

namespace Slx.Client.Windows;

internal static class IntegratedUpdateInstaller
{
    internal static async Task<int?> ExecuteAsync(string path, UpdateOffer offer, Guid id, Func<ProcessStartInfo, Task<int>> start)
    {
        UpdateSourcePolicy.Validate(offer); IntegratedUpdatePaths.File(path);
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous);
        if (file.Length != offer.InstallerBytes || !CryptographicOperations.FixedTimeEquals(await SHA256.HashDataAsync(file), Convert.FromHexString(offer.Sha256)))
            throw new ClientFault("UPDATE_INTEGRITY_FAILED");
        using var zone = new FileStream(path + ":Zone.Identifier", FileMode.Open, FileAccess.Read, FileShare.Read);
        if (zone.Length is <= 0 or > 2048) throw new ClientFault("UPDATE_INTEGRITY_FAILED");
        using var reader = new StreamReader(zone);
        if (!(await reader.ReadToEndAsync()).StartsWith("[ZoneTransfer]\r\nZoneId=3\r\n", StringComparison.Ordinal))
            throw new ClientFault("UPDATE_INTEGRITY_FAILED");
        try
        {
            return await start(new ProcessStartInfo(path)
            { UseShellExecute = true, Verb = "runas", WorkingDirectory = Path.GetDirectoryName(path)!, Arguments = IntegratedUpdatePolicy.InstallerArguments(id) });
        }
        catch (Win32Exception error) when (error.NativeErrorCode == 1223) { return null; }
    }
}
