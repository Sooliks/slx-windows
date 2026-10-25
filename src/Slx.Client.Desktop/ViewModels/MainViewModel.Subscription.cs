using System.IO;
using Slx.Client.Core;
using Slx.Client.Desktop.Localization;
namespace Slx.Client.Desktop.ViewModels;
public sealed partial class MainViewModel
{
    private CancellationTokenSource? subscriptionOperation;
    internal Func<string, CancellationToken, Task<SubscriptionSnapshot>>? SubscriptionFetchOverride { get; set; }
    private VpnProfile? selectedProfile;
    public VpnProfile? SelectedProfile
    {
        get => selectedProfile;
        set
        {
            if (!SetProperty(ref selectedProfile, value) || value == null) return;
            preferences = preferences with { SelectedProfileId = value.Id };
            SavePreferences(); MarkConnectionChanged();
        }
    }
    private Task ImportAsync() => FetchSubscriptionAsync(ImportLink, true);
    private Task RefreshAsync() => saved == null ? Task.CompletedTask : FetchSubscriptionAsync(saved.Url, false);
    private async Task FetchSubscriptionAsync(string url, bool importing)
    {
        if (IsBusy) return;
        CancelStartupRefresh();
        var owner = ++generation;
        IsBusy = true; ClearMessage();
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(35));
            subscriptionOperation = timeout;
            var snapshot = SubscriptionFetchOverride is not null ? await SubscriptionFetchOverride(url, timeout.Token)
                : await subscriptions.FetchAsync(url, store.GetOrCreateInstallationId(), Text.Catalog.Language, timeout.Token);
            if (owner != generation || disposed) return;
            ApplySubscription(url, snapshot);
            ImportVisible = false;
            OnPropertyChanged(nameof(IsOverlayVisible));
            OnPropertyChanged(nameof(SubscriptionTitle)); OnPropertyChanged(nameof(SubscriptionUpdated));
            OnPropertyChanged(nameof(SubscriptionExpiry));
            Notice(importing ? "Imported" : "Updated");
        }
        catch (Exception error) when (error is ClientFault or OperationCanceledException or IOException)
        {
            if (owner == generation) ShowFault(error is ClientFault fault ? fault.Code : "NETWORK_UNAVAILABLE");
        }
        finally { if (owner == generation) { subscriptionOperation = null; IsBusy = false; } }
    }
    private async Task RemoveSubscriptionAsync()
    {
        if (IsBusy) return;
        CancelStartupRefresh();
        RemoveBlocked = false;
        if (IsTunnelActive) await ChangeTunnelAsync("disconnect");
        // Without a fresh confirmed off state the saved link stays; the dialog says why (no silent no-op).
        if (IsTunnelActive || !statusFresh) { RemoveBlocked = !disposed; return; }
        try
        {
            // Render checks and UI fixtures never touch the user's stored subscription.
            if (persistPreferences) store.ClearSubscription();
            saved = null; Profiles.Clear(); SelectedProfile = null;
            ImportLink = ""; RemoveVisible = false; ClearMessage();
            OnPropertyChanged(nameof(IsOverlayVisible)); OnPropertyChanged(nameof(SubscriptionTitle)); NotifyState();
        }
        catch (ClientFault) { Notice("FaultStorage"); }
    }
}
