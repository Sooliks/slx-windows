using Slx.Client.Core;
namespace Slx.Client.Windows;
/// <summary>Local library presentation only; never expands discovery or a routing rule.</summary>
public sealed class LibraryApplicationOrder
{
    private static readonly Lazy<LibraryApplicationOrder> bundled = new(() => new(CatalogClient.Bundled()));
    private static readonly HashSet<string> Browsers = new(StringComparer.OrdinalIgnoreCase)
    { "chrome.exe", "browser.exe", "msedge.exe", "firefox.exe", "brave.exe", "opera.exe", "vivaldi.exe",
        "arc.exe", "librewolf.exe", "waterfox.exe", "zen.exe", "thorium.exe" };
    private static readonly HashSet<string> Games = new(StringComparer.OrdinalIgnoreCase)
    { "RobloxPlayerBeta.exe", "cs2.exe", "Rust.exe", "RustClient.exe", "FortniteClient-Win64-Shipping.exe",
        "VALORANT-Win64-Shipping.exe", "GenshinImpact.exe", "StarRail.exe", "Minecraft.Windows.exe",
        "steam.exe", "EpicGamesLauncher.exe", "Battle.net.exe", "RiotClientServices.exe", "EADesktop.exe", "GalaxyClient.exe" };
    private readonly Dictionary<string, int> catalogExecutables = new(StringComparer.OrdinalIgnoreCase);
    public static LibraryApplicationOrder Bundled => bundled.Value;
    public LibraryApplicationOrder(IReadOnlyList<CatalogApplication> catalog)
    {
        foreach (var app in catalog)
        foreach (var executable in app.Executables)
        {
            int rank = Category(app.Category);
            if (!catalogExecutables.TryGetValue(executable, out var previous) || rank < previous)
                catalogExecutables[executable] = rank;
        }
    }
    public int Priority(AppRule app) => Priority(app.ExecutablePath);
    public int Priority(CatalogApplication app) => Math.Min(Category(app.Category), app.Executables.Select(Priority).DefaultIfEmpty(4).Min());
    public IOrderedEnumerable<AppRule> Order(IEnumerable<AppRule> applications) => applications
        .OrderBy(Priority).ThenBy(app => app.Name.Trim(), StringComparer.OrdinalIgnoreCase)
        .ThenBy(app => app.Name.Trim(), StringComparer.Ordinal)
        .ThenBy(app => app.ExecutablePath, StringComparer.OrdinalIgnoreCase).ThenBy(app => app.ExecutablePath, StringComparer.Ordinal);
    private int Priority(string path)
    {
        var normalized = path.Replace('/', '\\');
        var executable = normalized[(normalized.LastIndexOf('\\') + 1)..];
        if (Browsers.Contains(executable)) return 0;
        if (executable.Equals("Telegram.exe", StringComparison.OrdinalIgnoreCase)) return 1;
        if (executable.Equals("Discord.exe", StringComparison.OrdinalIgnoreCase)) return 2;
        int catalog = catalogExecutables.GetValueOrDefault(executable, 4);
        if (catalog == 0) return 0;
        if (Games.Contains(executable) || normalized.Contains(@"\steamapps\common\", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains(@"\Epic Games\", StringComparison.OrdinalIgnoreCase)) return 3;
        return catalog;
    }
    private static int Category(string category) => category.ToLowerInvariant() switch
    { "browser" => 0, "game" or "games" or "launcher" => 3, _ => 4 };
}
