using System.IO;
using Slx.Client.Core;
using Slx.Client.Desktop.Localization;
namespace Slx.Client.Desktop.ViewModels;
public sealed partial class MainViewModel
{
    private VpnProfile? selectedProfile;
    public VpnProfile? SelectedProfile
    {
        get => selectedProfile;
        set
        {
            if (!SetProperty(ref selectedProfile, value) || value == null) return;
            preferences = preferences with { SelectedProfileId = value.Id };
            SavePreferences(); MarkRulesChanged();
        }
    }
    private Task ImportAsync() => FetchSubscriptionAsync(ImportLink, true);
    private Task RefreshAsync() => saved == null ? Task.CompletedTask : FetchSubscriptionAsync(saved.Url, false);
    private async Task FetchSubscriptionAsync(string url, bool importing)
    {
        if (IsBusy) return;
        var owner = ++generation;
        IsBusy = true; ClearMessage();
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(35));
            var snapshot = await subscriptions.FetchAsync(url, store.GetOrCreateInstallationId(), Text.Catalog.Language, timeout.Token);
            if (owner != generation || disposed) return;
            var next = new StoredSubscription(url, snapshot);
            store.SaveSubscription(next);
            saved = next;
            var previous = SelectedProfile?.Id;
            Profiles.Clear();
            foreach (var profile in snapshot.Profiles) Profiles.Add(profile);
            SelectedProfile = Profiles.FirstOrDefault(x => x.Id == previous)
                ?? Profiles.FirstOrDefault(x => x.Kind == "hysteria2") ?? Profiles.FirstOrDefault();
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
        finally { if (owner == generation) IsBusy = false; }
    }
    private async Task RemoveSubscriptionAsync()
    {
        if (IsBusy) return;
        if (IsTunnelActive) await ChangeTunnelAsync("disconnect");
        if (IsTunnelActive || !statusFresh) return;
        try
        {
            store.ClearSubscription(); saved = null; Profiles.Clear(); SelectedProfile = null;
            ImportLink = ""; RemoveVisible = false; ClearMessage();
            OnPropertyChanged(nameof(IsOverlayVisible)); OnPropertyChanged(nameof(SubscriptionTitle)); NotifyState();
        }
        catch (ClientFault) { Notice("FaultStorage"); }
    }
}
