namespace Slx.Client.Core;

public enum UpdateCheckState { Current, Available, Unavailable }
public sealed record UpdateCheckResult(UpdateCheckState State, UpdateOffer? Offer = null, string? ErrorCode = null);
public sealed record UpdateOffer(string Version, Uri ReleasePage, string AssetName, Uri InstallerUrl, long InstallerBytes, string Sha256);
public sealed record ReleaseUpdateMetadata(UpdateOffer Installer, Uri ChecksumUrl, long ChecksumBytes);

/// <summary>Only this public repository can supply update metadata and installer assets.</summary>
public static class UpdateSourcePolicy
{
    public const long MaxInstallerBytes = 300L * 1024 * 1024;
    public const string Repository = "https://github.com/Sooliks/slx-windows";
    public const string Feed = "https://api.github.com/repos/Sooliks/slx-windows/releases?per_page=20";
    public static string InstallerName(string version) => "SLX-VPN-" + version + "-win-x64-setup.exe";
    public static Uri ReleasePage(string version) => new(Repository + "/releases/tag/v" + Uri.EscapeDataString(version));
    public static bool AssetUrl(Uri uri, string version, string name) => Secure(uri) && uri.Host == "github.com"
        && uri.Query.Length == 0 && Uri.UnescapeDataString(uri.AbsolutePath) == "/Sooliks/slx-windows/releases/download/v" + version + "/" + name;
    public static bool Secure(Uri uri) => uri.IsAbsoluteUri && uri.Scheme == "https" && uri.IsDefaultPort
        && uri.UserInfo.Length == 0 && uri.Fragment.Length == 0;
    public static void Validate(UpdateOffer offer)
    {
        if (!ReleaseVersion.TryParse(offer.Version, out _) || offer.AssetName != InstallerName(offer.Version)
            || !Secure(offer.ReleasePage) || offer.ReleasePage != ReleasePage(offer.Version) || !AssetUrl(offer.InstallerUrl, offer.Version, offer.AssetName)
            || offer.InstallerBytes is <= 0 or > MaxInstallerBytes || offer.Sha256.Length != 64
            || !offer.Sha256.All(character => char.IsAsciiHexDigit(character))) throw new ClientFault("UPDATE_INVALID");
    }
}
