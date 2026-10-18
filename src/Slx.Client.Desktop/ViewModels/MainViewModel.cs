using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Slx.Client.Core;
using Slx.Client.Desktop.Localization;
using Slx.Client.Desktop.Presentation;
using Slx.Client.Windows;
namespace Slx.Client.Desktop.ViewModels;
public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly UserSettingsStore store = new();
    private readonly bool persistPreferences;
    private readonly SubscriptionClient subscriptions = new();
    private readonly ServiceClient service = new();
    private ClientPreferences preferences = new();
    private StoredSubscription? saved;
    private TunnelStatus? status;
    private string? lastServiceCode;
    private string page = "connection", search = "", importLink = "", messageKey = "";
    private bool busy, importVisible, discoveryVisible, removeVisible, rulesPending, statusFresh;
    public MainViewModel(bool loadStoredData = true, StoredSubscription? initialSubscription = null, ClientPreferences? initialPreferences = null)
    {
        persistPreferences = loadStoredData;
        if (!loadStoredData) { saved = initialSubscription; preferences = initialPreferences ?? new(); }
        try { if (loadStoredData) { preferences = store.LoadPreferences(); saved = store.LoadSubscription(); } }
        catch (ClientFault) { messageKey = "FaultStorage"; }
        Text.Catalog.SetLanguage(preferences.Language);
        ThemeManager.Apply(preferences.Theme);
        foreach (var rule in preferences.Applications ?? []) Attach(rule);
        Profiles = new(saved?.Snapshot.Profiles ?? []);
        selectedProfile = Profiles.FirstOrDefault(x => x.Id == preferences.SelectedProfileId)
            ?? Profiles.FirstOrDefault(x => x.Kind == "hysteria2") ?? Profiles.FirstOrDefault();
        NavigateCommand = new RelayCommand<string>(value => { Page = value ?? "connection"; ClearMessage(); });
        OpenImportCommand = new RelayCommand(() => { CancelStartupRefresh(); ImportLink = ""; ImportVisible = true; ClearMessage(); });
        CancelOverlayCommand = new RelayCommand(CloseOverlay);
        ImportCommand = new AsyncRelayCommand(ImportAsync, () => !IsBusy);
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => saved != null && !IsBusy);
        ToggleConnectionCommand = new AsyncRelayCommand(ToggleAsync, AsyncRelayCommandOptions.AllowConcurrentExecutions);
        ReconnectCommand = new AsyncRelayCommand(() => ChangeTunnelAsync("reconnect"), () => CanReconnect);
        AddExecutableCommand = new RelayCommand(AddExecutable);
        DiscoverCommand = new RelayCommand(() => _ = DiscoverApplicationsAsync(), () => !Discovering);
        AddDiscoveredCommand = new RelayCommand<AppRule>(AddDiscovered);
        RemoveAppCommand = new RelayCommand<AppItemViewModel>(RemoveApp);
        OpenAccountCommand = new RelayCommand(() => OpenLink("https://slxvpn.com/account"));
        OpenSupportCommand = new RelayCommand(() => OpenLink("https://t.me/slxvpn?direct"));
        OpenSourceCommand = new RelayCommand(() => OpenLink("https://github.com/Sooliks/slx-windows"));
        OpenWebsiteCommand = new RelayCommand(() => OpenLink("https://slxvpn.com"));
        AskRemoveCommand = new RelayCommand(() => RemoveVisible = true, () => saved != null && !IsBusy);
        ConfirmRemoveCommand = new AsyncRelayCommand(RemoveSubscriptionAsync);
        Text.Catalog.PropertyChanged += LanguageChanged;
    }
    public ObservableCollection<AppItemViewModel> Applications { get; } = [];
    public ObservableCollection<VpnProfile> Profiles { get; }
    public ObservableCollection<AppRule> Discovered { get; } = [];
    public IEnumerable<AppItemViewModel> FilteredApplications => Applications.Where(x =>
        x.Name.Contains(Search, StringComparison.CurrentCultureIgnoreCase));
    public IEnumerable<AppItemViewModel> SelectedApplications => Applications.Where(x => x.Enabled).Take(4);
    public bool HasApplications => Applications.Count > 0;
    public bool HasSelectedApplications => Applications.Any(x => x.Enabled);
    public bool HasSearchResults => FilteredApplications.Any();
    public bool HasDiscovered => Discovered.Count > 0;
    public string Page { get => page; set { if (SetProperty(ref page, value) && value == "applications" && persistPreferences) _ = DiscoverApplicationsAsync(); } }
    public string Search { get => search; set { if (SetProperty(ref search, value)) { OnPropertyChanged(nameof(FilteredApplications)); OnPropertyChanged(nameof(HasSearchResults)); } } }
    public string ImportLink { get => importLink; set => SetProperty(ref importLink, value); }
    public bool ImportVisible { get => importVisible; set { if (SetProperty(ref importVisible, value)) OnPropertyChanged(nameof(IsOverlayVisible)); } }
    public bool DiscoveryVisible { get => discoveryVisible; set { if (SetProperty(ref discoveryVisible, value)) OnPropertyChanged(nameof(IsOverlayVisible)); } }
    public bool RemoveVisible { get => removeVisible; set { if (SetProperty(ref removeVisible, value)) OnPropertyChanged(nameof(IsOverlayVisible)); } }
    public bool IsOverlayVisible => ImportVisible || DiscoveryVisible || RemoveVisible;
    public bool IsBusy { get => busy; private set { if (SetProperty(ref busy, value)) NotifyState(); } }
    public bool RulesPending { get => rulesPending; private set => SetProperty(ref rulesPending, value); }
    public bool HasSubscription => saved != null;
    public bool NoSubscription => !HasSubscription;
    public bool ShowMessage => messageKey.Length > 0;
    public string Message => Text.Catalog[messageKey];
    public string SubscriptionTitle => saved?.Snapshot.Title ?? Text.Catalog["Subscription"];
    public string SubscriptionUpdated => saved?.Snapshot.FetchedAt.ToLocalTime().ToString("g") ?? "—";
    public string SubscriptionExpiry => saved?.Snapshot.ExpiresAt?.ToLocalTime().ToString("d") ?? Text.Catalog["ExpiryUnknown"];
    public bool IsConnected => statusFresh && status?.Phase == TunnelPhase.Connected && status.ProbePassed;
    public bool IsTunnelActive => status?.Phase is TunnelPhase.Connected or TunnelPhase.Connecting or TunnelPhase.Reconnecting;
    public bool CanReconnect => statusFresh && HasSubscription && IsTunnelActive && !IsBusy;
    public bool CanEditRouting => !IsBusy;
    public bool CanConnect => (HasSubscription && statusFresh) || IsTunnelActive || IsBusy;
    public string ConnectionAction => Text.Catalog[IsTunnelActive || IsBusy ? "Disconnect" : "Connect"];
    public string StatusText => Text.Catalog[!statusFresh ? lastServiceCode == null ? "StatusUnknown" : status == null ? "StatusUnavailable" : "StatusStale" : status?.Phase switch
    {
        TunnelPhase.Connected when status.ProbePassed => "Connected", TunnelPhase.Connecting => "Connecting",
        TunnelPhase.Reconnecting => "Reconnecting", TunnelPhase.Stopping => "Stopping",
        TunnelPhase.Faulted => "ConnectionFailed", _ => "NotConnected"
    }];
    public string? LastServiceCode => lastServiceCode;
    public bool HasServiceCode => lastServiceCode != null;
    public string ServiceCodeLabel => lastServiceCode == null ? "" : $"{Text.Catalog["ErrorCode"]}: {lastServiceCode}";
    public string StatusDetail => Text.Catalog[!statusFresh ? ServiceStateCopy.DetailKey(lastServiceCode) : "NoMetrics"];
    public string VersionLabel => "SLX VPN · " + (System.Reflection.Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false).OfType<System.Reflection.AssemblyInformationalVersionAttribute>().FirstOrDefault()?.InformationalVersion.Split('+')[0] ?? "alpha");
    public RelayCommand<string> NavigateCommand { get; }
    public RelayCommand OpenImportCommand { get; }
    public RelayCommand CancelOverlayCommand { get; }
    public AsyncRelayCommand ImportCommand { get; }
    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand ToggleConnectionCommand { get; }
    public AsyncRelayCommand ReconnectCommand { get; }
    public RelayCommand AddExecutableCommand { get; }
    public RelayCommand DiscoverCommand { get; }
    public RelayCommand<AppRule> AddDiscoveredCommand { get; }
    public RelayCommand<AppItemViewModel> RemoveAppCommand { get; }
    public RelayCommand OpenAccountCommand { get; }
    public RelayCommand OpenSupportCommand { get; }
    public RelayCommand OpenSourceCommand { get; }
    public RelayCommand OpenWebsiteCommand { get; }
    public RelayCommand AskRemoveCommand { get; }
    public AsyncRelayCommand ConfirmRemoveCommand { get; }
    private void LanguageChanged(object? sender, PropertyChangedEventArgs e) => OnPropertyChanged("");
    private void ClearMessage() { messageKey = ""; OnPropertyChanged(nameof(Message)); OnPropertyChanged(nameof(ShowMessage)); }
    private void Notice(string key) { messageKey = key; OnPropertyChanged(nameof(Message)); OnPropertyChanged(nameof(ShowMessage)); }
    private void NotifyState()
    {
        foreach (var name in new[] { nameof(IsConnected), nameof(CanEditRouting), nameof(CanConnect), nameof(CanReconnect), nameof(ConnectionAction), nameof(StatusText), nameof(StatusDetail), nameof(LastServiceCode), nameof(HasServiceCode), nameof(ServiceCodeLabel), nameof(HasSubscription), nameof(NoSubscription) }) OnPropertyChanged(name);
        ImportCommand?.NotifyCanExecuteChanged(); RefreshCommand?.NotifyCanExecuteChanged();
        ReconnectCommand?.NotifyCanExecuteChanged(); AskRemoveCommand?.NotifyCanExecuteChanged();
    }
}
