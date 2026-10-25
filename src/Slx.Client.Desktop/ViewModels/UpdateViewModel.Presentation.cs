using System.Globalization;
using Slx.Client.Desktop.Localization;
namespace Slx.Client.Desktop.ViewModels;
public sealed partial class UpdateViewModel
{
    public bool HasOffer => offer != null;
    public string AutomationCaption => Text.Catalog[HasIntegratedUpdate ? "UpdateChecksAndDownloads" : "UpdatesAutomatic"];
    public bool IsOfferDeferred => HasOffer && !ShowBanner;
    public bool IsInstallBlocked => prepared != null && !busy && !installerOpened && !canInstall()
        && !(HasIntegratedUpdate && connectionActive?.Invoke() == true);
    public bool ShowInstallHint => prepared != null && !busy && !installerOpened
        && !confirmationVisible && !IsInstallCountdown && (!HasIntegratedUpdate || connectionActive?.Invoke() != true)
        && (!IsInstallBlocked || stateKey is not ("UpdateReady" or "UpdateFound" or "UpdateDisconnectFirst"));
    public string OfferTitle => offer == null ? "" : Format("UpdateAvailable", offer.Version);
    public string CurrentVersionCaption => Format("UpdateInstalledVersion", version);
    public string AutomationPrivacy => Text.Catalog[HasIntegratedUpdate ? "UpdateIntegratedPrivacy" : "UpdatesPrivacy"];
    public string ProgressText => Format("UpdateTransferProgress", progress);
    public string LaterText => Text.Catalog[IsDownloading ? "UpdateActionHide" : "UpdateActionLater"];
    public string LaterName => Text.Catalog[IsDownloading ? "UpdateHideDownload" : "UpdateLater"];
    public string LaterHint => Text.Catalog[IsDownloading ? "UpdateHideDownloadHint" : "UpdateLaterHint"];
    public string ActionText => Text.Catalog[prepared != null ? HasIntegratedUpdate ? "UpdateActionUpdate" : "UpdateActionInstall" : "UpdateActionDownload"];
    public string ActionName => offer == null ? ActionText : Format(prepared != null ? "UpdateInstallVersion" : "UpdateDownloadVersion", offer.Version);
    public string BannerDescription
    {
        get
        {
            if (confirmationVisible) return Text.Catalog["UpdateActiveWarning"];
            if (IsInstallCountdown) return CountdownText;
            if (HasIntegratedUpdate && IsDownloading) return Text.Catalog["UpdateBackgroundDownloading"];
            if (prepared != null && !busy && stateKey is "UpdateReady" or "UpdateFound" or "UpdateDisconnectFirst")
            {
                if (HasIntegratedUpdate && connectionActive?.Invoke() == true) return Text.Catalog["UpdateReadyActive"];
                if (IsInstallBlocked) return InstallHint;
                return Text.Catalog["UpdateReady"];
            }
            return Text.Catalog[stateKey];
        }
    }
    public string InstallHint => Text.Catalog[HasIntegratedUpdate && connectionActive?.Invoke() == true ? "UpdateActiveWarning"
        : IsInstallBlocked ? installBlockReason?.Invoke() ?? "UpdateDisconnectFirst"
        : HasIntegratedUpdate ? "UpdateIntegratedHint" : "UpdateInstallHint"];
    private static string CheckFailureKey(string? code) => code switch
    {
        "UPDATE_TIMEOUT" => "UpdateCheckTimeout",
        "UPDATE_INVALID" => "UpdateCheckInvalid",
        _ => "UpdateCheckFailed"
    };
    private static string Format(string key, params object[] arguments) =>
        string.Format(CultureInfo.CurrentCulture, Text.Catalog[key], arguments);
}
