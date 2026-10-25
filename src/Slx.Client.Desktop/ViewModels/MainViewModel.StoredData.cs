using Slx.Client.Core;
using Slx.Client.Windows;
namespace Slx.Client.Desktop.ViewModels;

public sealed partial class MainViewModel
{
    public bool HasPreferencesFault { get; private set; }
    internal static (ClientPreferences Preferences, StoredSubscription? Subscription, bool PreferencesFailed, bool SubscriptionFailed)
        ReadStoredData(UserSettingsStore settings)
    {
        ClientPreferences preferences = new(); StoredSubscription? subscription = null;
        bool preferencesFailed = false, subscriptionFailed = false;
        try { preferences = settings.LoadPreferences(); }
        catch (ClientFault) { preferencesFailed = true; }
        try { subscription = settings.LoadSubscription(); }
        catch (ClientFault) { subscriptionFailed = true; }
        return (preferences, subscription, preferencesFailed, subscriptionFailed);
    }
}
