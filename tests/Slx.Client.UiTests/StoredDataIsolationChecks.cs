using System.IO;
using Slx.Client.Core;
using Slx.Client.Desktop.ViewModels;
using Slx.Client.Windows;
using Xunit;
namespace Slx.Client.UiTests;

internal static class StoredDataIsolationChecks
{
    public static void Run()
    {
        var directory = Path.Combine(Path.GetTempPath(), "SLX-StoredData-Tests", Guid.NewGuid().ToString("N"));
        try
        {
            var store = new UserSettingsStore(directory);
            var subscription = new StoredSubscription("https://slxvpn.com/sub/" + new string('a', 24) + "/" + new string('b', 32),
                SubscriptionParser.Parse("hysteria2://not-a-real-secret@vpn.example.com:8444/?sni=vpn.example.com#Synthetic"));
            store.SaveSubscription(subscription);
            var installation = store.GetOrCreateInstallationId();
            var vault = File.ReadAllBytes(Path.Combine(directory, "profile.dat"));
            var preferencesPath = Path.Combine(directory, "preferences.json");
            const string corrupt = "{\"Applications\":[null]}";
            File.WriteAllText(preferencesPath, corrupt);
            var loaded = MainViewModel.ReadStoredData(store);
            Assert.True(loaded.PreferencesFailed); Assert.False(loaded.SubscriptionFailed);
            Assert.NotNull(loaded.Subscription); Assert.Equal(subscription.Url, loaded.Subscription.Url);
            Assert.Equal(subscription.Snapshot.Profiles, loaded.Subscription.Snapshot.Profiles);
            Assert.Equal(installation, store.GetOrCreateInstallationId());
            Assert.Equal(corrupt, File.ReadAllText(preferencesPath));
            Assert.Equal(vault, File.ReadAllBytes(Path.Combine(directory, "profile.dat")));

            File.WriteAllText(preferencesPath, "{}");
            store = new UserSettingsStore(directory);
            store.LoadPreferences(); store.SavePreferences(new ClientPreferences(Language: "en", Theme: "terminal"));
            File.WriteAllBytes(Path.Combine(directory, "profile.dat"), [1, 2, 3]);
            loaded = MainViewModel.ReadStoredData(store);
            Assert.False(loaded.PreferencesFailed); Assert.True(loaded.SubscriptionFailed);
            Assert.Equal("en", loaded.Preferences.Language); Assert.Equal("terminal", loaded.Preferences.Theme);
            Assert.Null(loaded.Subscription);
        }
        finally
        {
            var allowed = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "SLX-StoredData-Tests")) + Path.DirectorySeparatorChar;
            var target = Path.GetFullPath(directory);
            if (target.StartsWith(allowed, StringComparison.OrdinalIgnoreCase) && Directory.Exists(target)) Directory.Delete(target, true);
        }
    }
}
