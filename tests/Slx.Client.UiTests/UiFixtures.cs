using Slx.Client.Core;
using Slx.Client.Desktop.ViewModels;
namespace Slx.Client.UiTests;
internal static class UiFixtures
{
    public static void AllowSyntheticApplications(MainViewModel model)=>model.LibraryValidateOverride=(paths,_)=>Task.FromResult(paths);
    public static readonly VpnProfile Primary = new("ui-primary", "SLX · Москва → Амстердам · Hysteria2", "hysteria2", "vpn.example.invalid", 8444, "not-a-real-profile-secret", "vpn.example.invalid");
    public static readonly VpnProfile Alternative = new("ui-alternative", "SLX · Москва → Амстердам · VLESS · длинное имя резервного подключения", "vless", "vpn.example.invalid", 8443, "not-a-real-profile-secret", "vpn.example.invalid");
    public static StoredSubscription Subscription => new("https://slxvpn.com/sub/synthetic-ui-fixture", new("SLX VPN", new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero), [Primary, Alternative], new DateTimeOffset(2026, 10, 18, 0, 0, 0, TimeSpan.Zero)));
    public static ClientPreferences Preferences(RoutingMode mode) => new(Applications:
    [new(Guid.NewGuid(), "Messenger", @"C:\SLX UI Fixture\Messenger.exe"), new(Guid.NewGuid(), "A browser with an intentionally very long application name for layout", @"C:\SLX UI Fixture\Browser.exe")], Mode: mode);
}
