using Slx.Client.Core;

namespace Slx.Client.Windows;

public sealed partial class ReleaseUpdateClient
{
    private const int MaxStagedCandidates = 64;
    /// <summary>
    /// Returns an installer for exactly this offer that an earlier session already staged, so the same release is not
    /// downloaded again. Only our protected staging layout is considered, and the file must still match the published
    /// size and SHA-256. A missing or damaged candidate is ignored; installation re-verifies the file before launch.
    /// </summary>
    public async Task<PreparedUpdate?> FindPreparedAsync(UpdateOffer offer, CancellationToken cancellationToken = default)
    {
        UpdateSourcePolicy.Validate(offer);
        if (!Directory.Exists(root)) return null;
        try { EnsureLocalDirectory(root); }
        catch (ClientFault) { return null; }
        IEnumerable<string> directories;
        try { directories = Directory.EnumerateDirectories(root).Take(MaxStagedCandidates).ToArray(); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { return null; }
        foreach (var directory in directories)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Guid.TryParseExact(Path.GetFileName(directory), "N", out _)) continue;
            var installer = Path.Combine(directory, offer.AssetName);
            try
            {
                if (!File.Exists(installer)) continue;
                EnsureLocalDirectory(directory);
                await VerifyAsync(installer, offer, cancellationToken);
                await EnsureZoneAsync(installer, offer, cancellationToken);
                return new(offer, installer);
            }
            catch (ClientFault) { }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
        }
        return null;
    }
    // The installer must keep Internet-zone metadata so Windows SmartScreen and UAC treat it as downloaded.
    private static async Task EnsureZoneAsync(string installer, UpdateOffer offer, CancellationToken cancellationToken)
    {
        var zone = installer + ":Zone.Identifier";
        try
        {
            var text = await File.ReadAllTextAsync(zone, cancellationToken);
            if (text.Length <= 2048 && text.StartsWith("[ZoneTransfer]\r\nZoneId=3\r\n", StringComparison.Ordinal)) return;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
        await File.WriteAllTextAsync(zone, "[ZoneTransfer]\r\nZoneId=3\r\nHostUrl=" + offer.InstallerUrl.AbsoluteUri + "\r\n", cancellationToken);
    }
}
