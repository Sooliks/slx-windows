using Slx.Client.Core;

namespace Slx.Client.Windows;

public enum IntegratedUpdatePhase
{ Staged, WaitingForApplication, AwaitingPermission, Installing, Verifying, Succeeded, Cancelled, Failed, RestartRequired, Unknown }
public sealed record IntegratedUpdateHandoff(Guid SessionId, int HostProcessId);
public sealed record IntegratedUpdateResult(Guid SessionId, string Version, IntegratedUpdatePhase Phase,
    DateTimeOffset UpdatedAt, int? InstallerExitCode = null, bool ResumeConnection = false, string? ResumeFingerprint = null,
    string? SourceVersion = null, DateTimeOffset? BootStartedAt = null);
internal sealed record IntegratedUpdateJob(Guid SessionId, UpdateOffer Offer, string InstallerPath, string InstallationRoot,
    int OriginalProcessId, DateTimeOffset OriginalProcessStartedAt, DateTimeOffset CreatedAt, string Language,
    bool ResumeConnection, string? ResumeFingerprint, string SourceVersion);
internal sealed record IntegratedUpdateHostIdentity(int ProcessId, DateTimeOffset StartedAt);
internal sealed record IntegratedUpdateBundle(IReadOnlyList<IntegratedUpdateBundleFile> Files);
internal sealed record IntegratedUpdateBundleFile(string Name, long Size, string Sha256);

public static class IntegratedUpdatePolicy
{
    public static string InstallerArguments(Guid sessionId)
    {
        if (sessionId == Guid.Empty) throw new ClientFault("UPDATE_INVALID");
        return "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /RESTARTEXITCODE=3010 /SLXUPDATE=" + sessionId.ToString("N");
    }
    public static bool CanResume(IntegratedUpdateResult value, string installedVersion, string fingerprint) =>
        CanResumeAt(value, installedVersion, fingerprint, DateTimeOffset.UtcNow);
    internal static bool CanResumeAt(IntegratedUpdateResult value, string installedVersion, string fingerprint, DateTimeOffset now) =>
        (value.Phase == IntegratedUpdatePhase.Succeeded && value.Version == installedVersion
            || value.Phase == IntegratedUpdatePhase.Cancelled && value.SourceVersion == installedVersion) && value.ResumeConnection
        && Fingerprint(value.ResumeFingerprint) && value.ResumeFingerprint == fingerprint
        && value.UpdatedAt >= now.AddMinutes(-5) && value.UpdatedAt <= now.AddMinutes(1);
    internal static bool Fingerprint(string? value) => value is { Length: 64 } && value.All(char.IsAsciiHexDigit);
    internal static string FileVersion(string version)
    {
        if (!ReleaseVersion.TryParse(version, out _)) throw new ClientFault("UPDATE_INVALID");
        var pieces = version.Split('-');
        if (pieces.Length == 1) return pieces[0] + ".0";
        var suffix = pieces[1].Split('.');
        if (suffix.Length != 2 || suffix[0] is not ("alpha" or "beta" or "rc") || !ushort.TryParse(suffix[1], out var number))
            throw new ClientFault("UPDATE_INVALID");
        return pieces[0] + "." + number;
    }
}
