using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using Slx.Client.Core;

namespace Slx.Client.Windows;

public sealed class PreparedUpdate
{
    public UpdateOffer Offer { get; }
    public string InstallerPath { get; }
    internal PreparedUpdate(UpdateOffer offer, string path) { Offer = offer; InstallerPath = path; }
}
public sealed partial class ReleaseUpdateClient
{
    public async Task<PreparedUpdate> DownloadAsync(UpdateOffer offer, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        UpdateSourcePolicy.Validate(offer);
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(downloadTimeout);
        string? partial = null; string? installer = null; var completed = false;
        try
        {
            // The same verified release staged earlier is reused instead of being downloaded again.
            if (await FindPreparedAsync(offer, budget.Token) is { } staged) { progress?.Report(100); completed = true; return staged; }
            var directory = Path.Combine(root, Guid.NewGuid().ToString("N"));
            CreateStagingDirectory(directory);
            partial = Path.Combine(directory, "download.part");
            installer = Path.Combine(directory, offer.AssetName);
            progress?.Report(0);
            using (var output = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.Asynchronous))
            {
                using var transport = new UpdateTransport(createHandler());
                await transport.CopyAsync(offer.InstallerUrl, output, offer.InstallerBytes, offer.InstallerBytes, true, progress, budget.Token);
                await output.FlushAsync(budget.Token);
            }
            await VerifyAsync(partial, offer, budget.Token);
            File.Move(partial, installer);
            await File.WriteAllTextAsync(installer + ":Zone.Identifier", "[ZoneTransfer]\r\nZoneId=3\r\nHostUrl=" + offer.InstallerUrl.AbsoluteUri + "\r\n", budget.Token);
            completed = true;
            return new(offer, installer);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { throw new ClientFault("UPDATE_UNAVAILABLE"); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Net.Http.HttpRequestException)
        { throw new ClientFault("UPDATE_DOWNLOAD_FAILED"); }
        finally
        {
            DeletePartial(partial);
            if (!completed) DeletePartial(installer);
        }
    }
    private static async Task VerifyAsync(string path, UpdateOffer offer, CancellationToken cancellationToken)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous);
        await VerifyAsync(file, offer, cancellationToken);
    }
    private static async Task VerifyAsync(FileStream file, UpdateOffer offer, CancellationToken cancellationToken)
    {
        if (file.Length != offer.InstallerBytes || (File.GetAttributes(file.Name) & FileAttributes.ReparsePoint) != 0
            || !CryptographicOperations.FixedTimeEquals(await SHA256.HashDataAsync(file, cancellationToken), Convert.FromHexString(offer.Sha256)))
            throw new ClientFault("UPDATE_INTEGRITY_FAILED");
    }
    private static void EnsureLocalDirectory(string directory)
    {
        for (var current = new DirectoryInfo(directory); current is not null; current = current.Parent)
        {
            if ((current.Attributes & FileAttributes.ReparsePoint) != 0) throw new ClientFault("UPDATE_DOWNLOAD_FAILED");
        }
    }
    private void CreateStagingDirectory(string directory)
    {
        Directory.CreateDirectory(root);
        EnsureLocalDirectory(root);
        using var identity = WindowsIdentity.GetCurrent();
        var user = identity.User ?? throw new ClientFault("UPDATE_DOWNLOAD_FAILED");
        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(true, false);
        foreach (var sid in new[] { user, new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null) })
            security.AddAccessRule(new(sid, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None, AccessControlType.Allow));
        new DirectoryInfo(directory).Create(security);
        EnsureLocalDirectory(directory);
    }
    private static void DeletePartial(string? path)
    {
        if (path is null) return;
        try { File.Delete(path); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
    }
}
