using Slx.Client.Core;
using Slx.Client.Desktop.Localization;
namespace Slx.Client.Desktop.ViewModels;
public sealed partial class MainViewModel
{
    private CancellationTokenSource? startupRefresh;
    private bool startupRefreshStarted, subscriptionRefreshing, startupRefreshFailed, startupRefreshFinished;
    internal Func<string, CancellationToken, Task<SubscriptionSnapshot>>? StartupFetchOverride { get; set; }
    internal TimeSpan StartupRefreshBudget { get; set; } = TimeSpan.FromSeconds(20);
    public bool IsSubscriptionRefreshing { get => subscriptionRefreshing; private set { if (SetProperty(ref subscriptionRefreshing, value)) OnPropertyChanged(nameof(SubscriptionRefreshHint)); } }
    public string SubscriptionRefreshHint => Text.Catalog[IsSubscriptionRefreshing ? "RefreshingProfiles" : startupRefreshFailed ? "CachedProfiles" : "ProfilesAutoRefresh"];
    internal async Task RefreshOnStartupAsync()
    {
        if (startupRefreshStarted || disposed) return;
        startupRefreshStarted = true;
        if (saved is null) { startupRefreshFinished = true; return; }
        var source = saved;
        using var budget = new CancellationTokenSource(StartupRefreshBudget);
        startupRefresh = budget;
        IsSubscriptionRefreshing = true;
        try
        {
            var snapshot = StartupFetchOverride != null ? await StartupFetchOverride(source.Url, budget.Token)
                : await subscriptions.FetchAsync(source.Url, store.GetOrCreateInstallationId(), Text.Catalog.Language, budget.Token);
            if (disposed || !ReferenceEquals(saved, source)) return;
            if (budget.IsCancellationRequested || IsBusy || IsTunnelActive)
            {
                if (ReferenceEquals(startupRefresh, budget)) startupRefreshFailed = true;
                return;
            }
            ApplySubscription(source.Url, snapshot);
        }
        catch (Exception error) when (error is ClientFault or OperationCanceledException or System.IO.IOException)
        { if (!disposed && ReferenceEquals(startupRefresh, budget) && ReferenceEquals(saved, source)) startupRefreshFailed = true; }
        finally
        {
            if (ReferenceEquals(startupRefresh, budget)) startupRefresh = null;
            IsSubscriptionRefreshing = false;
            startupRefreshFinished = true;
            await TryStartupConnectionAsync();
        }
    }
    private void CancelStartupRefresh() { CancelStartupConnection(); startupRefresh?.Cancel(); startupRefresh = null; }
    private void ApplySubscription(string url, SubscriptionSnapshot snapshot)
    {
        var next = new StoredSubscription(url, snapshot);
        if (persistPreferences) store.SaveSubscription(next);
        var previous = SelectedProfile?.Id;
        saved = next;
        Profiles.Clear();
        foreach (var profile in snapshot.Profiles) Profiles.Add(profile);
        SelectedProfile = Profiles.FirstOrDefault(profile => profile.Id == previous)
            ?? Profiles.FirstOrDefault(profile => profile.Kind == "hysteria2") ?? Profiles.FirstOrDefault();
        startupRefreshFailed = false;
        foreach (var name in new[] { nameof(SubscriptionTitle), nameof(SubscriptionUpdated), nameof(SubscriptionExpiry), nameof(SubscriptionRefreshHint) }) OnPropertyChanged(name);
        NotifyState();
    }
}
