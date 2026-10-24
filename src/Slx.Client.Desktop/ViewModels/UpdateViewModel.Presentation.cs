using System.Globalization;
using Slx.Client.Desktop.Localization;
namespace Slx.Client.Desktop.ViewModels;
public sealed partial class UpdateViewModel
{
    public bool HasOffer => offer != null;
    public bool IsOfferDeferred => HasOffer && !ShowBanner;
    public bool IsInstallBlocked => prepared != null && !busy && !installerOpened && !canInstall();
    public bool ShowInstallHint => prepared != null && !busy && !installerOpened
        && (!IsInstallBlocked || stateKey is not ("UpdateReady" or "UpdateFound" or "UpdateDisconnectFirst"));
    public string OfferTitle => offer == null ? "" : Format("UpdateAvailable", offer.Version);
    public string CurrentVersionCaption => Format("UpdateInstalledVersion", version);
    public string ProgressText => Format("UpdateTransferProgress", progress);
    public string LaterText => Text.Catalog[IsDownloading ? "UpdateActionHide" : "UpdateActionLater"];
    public string LaterName => Text.Catalog[IsDownloading ? "UpdateHideDownload" : "UpdateLater"];
    public string LaterHint => Text.Catalog[IsDownloading ? "UpdateHideDownloadHint" : "UpdateLaterHint"];
    public string ActionText => Text.Catalog[prepared != null ? "UpdateActionInstall" : "UpdateActionDownload"];
    public string ActionName => offer == null ? ActionText : Format(prepared != null ? "UpdateInstallVersion" : "UpdateDownloadVersion", offer.Version);
    public string BannerDescription => stateKey == "UpdateDisconnectFirst" && prepared != null && !busy && !IsInstallBlocked
        ? Text.Catalog["UpdateReady"]
        : IsInstallBlocked && (stateKey is "UpdateReady" or "UpdateFound" or "UpdateDisconnectFirst")
            ? InstallHint : Text.Catalog[stateKey];
    public string InstallHint => Text.Catalog[IsInstallBlocked
        ? installBlockReason?.Invoke() ?? "UpdateDisconnectFirst" : "UpdateInstallHint"];
    private static string CheckFailureKey(string? code) => code switch
    {
        "UPDATE_TIMEOUT" => "UpdateCheckTimeout",
        "UPDATE_INVALID" => "UpdateCheckInvalid",
        _ => "UpdateCheckFailed"
    };
    private static string Format(string key, params object[] arguments) =>
        string.Format(CultureInfo.CurrentCulture, Text.Catalog[key], arguments);
}
