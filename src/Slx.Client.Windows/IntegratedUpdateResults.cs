using System.Security.Cryptography;
using System.Text.Json;
using Slx.Client.Core;

namespace Slx.Client.Windows;

public static class IntegratedUpdateResults
{
    public static Guid? FindPending(string installedVersion)
    {
        try
        {
            if (!Directory.Exists(IntegratedUpdatePaths.Root)) return null;
            IntegratedUpdatePaths.NoReparse(IntegratedUpdatePaths.Root);
            var results = new List<IntegratedUpdateResult?>();
            foreach (var directory in Directory.EnumerateDirectories(IntegratedUpdatePaths.Root).Take(64))
            {
                if (!Guid.TryParseExact(Path.GetFileName(directory), "N", out var id) || id == Guid.Empty) continue;
                results.Add(Read(id, installedVersion));
            }
            return LatestPending(results)?.SessionId;
        }
        catch (Exception error) when (error is ClientFault or IOException or UnauthorizedAccessException or ArgumentException) { return null; }
    }
    internal static IntegratedUpdateResult? LatestPending(IEnumerable<IntegratedUpdateResult?> results) => results
        .Where(value => value is not null).OrderByDescending(value => value!.UpdatedAt).ThenBy(value => value!.SessionId).FirstOrDefault();
    internal static bool WithinRetention(IntegratedUpdateResult value, DateTimeOffset now) => value.UpdatedAt <= now.AddMinutes(1)
        && (value.UpdatedAt >= now.AddDays(-1) || value.Phase == IntegratedUpdatePhase.RestartRequired && value.InstallerExitCode == 3010
            && value.BootStartedAt is { } boot && boot.Offset == TimeSpan.Zero && boot > DateTimeOffset.MinValue && boot <= value.UpdatedAt);
    public static IntegratedUpdateResult? Read(Guid id, string installedVersion)
    {
        try
        {
            var job = IntegratedUpdateStore.Read<IntegratedUpdateJob>(id, "job.bin");
            var value = IntegratedUpdateStore.Read<IntegratedUpdateResult>(id, "result.bin");
            // Result parsing does not require the package or old installation files to survive the completed update.
            if (job.Offer is null || !ReleaseVersion.TryParse(job.SourceVersion, out _) || job.ResumeConnection
                && !IntegratedUpdatePolicy.Fingerprint(job.ResumeFingerprint)) return null;
            UpdateSourcePolicy.Validate(job.Offer);
            if (job.SessionId != id || value.SessionId != id || value.Version != job.Offer.Version || value.SourceVersion != job.SourceVersion
                || !Enum.IsDefined(value.Phase) || value.UpdatedAt < job.CreatedAt || !WithinRetention(value, DateTimeOffset.UtcNow)
                || installedVersion != value.Version && installedVersion != value.SourceVersion
                || value.ResumeConnection != job.ResumeConnection || value.ResumeFingerprint != job.ResumeFingerprint)
                return null;
            return value;
        }
        catch (Exception error) when (error is ClientFault or IOException or UnauthorizedAccessException or CryptographicException or JsonException
            or ArgumentException or NotSupportedException) { return null; }
    }
    public static void Consume(Guid id)
    {
        string path = Path.Combine(IntegratedUpdatePaths.Session(id), "result.bin");
        if (!File.Exists(path)) return;
        IntegratedUpdatePaths.File(path); File.Delete(path);
    }
}
