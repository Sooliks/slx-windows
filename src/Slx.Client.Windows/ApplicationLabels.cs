using Slx.Client.Core;
namespace Slx.Client.Windows;

/// <summary>Presentation/search only. Never grants routing permission or expands an application.</summary>
public static class ApplicationLabels
{
    public static bool IsRiotClient(string path) => Path.GetFileName(path) is { } file &&
        new[] { "RiotClientServices.exe", "Riot Client.exe", "RiotClientUx.exe", "RiotClientUxRender.exe" }
            .Contains(file, StringComparer.OrdinalIgnoreCase);
    public static string DisplayName(AppRule rule) => IsRiotClient(rule.ExecutablePath) ? "Riot Client" : rule.Name;
    public static bool Matches(AppRule rule, string query)
    {
        query = query.Trim();
        return rule.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)
            || rule.ExecutablePath.Contains(query, StringComparison.OrdinalIgnoreCase)
            || DisplayName(rule).Contains(query, StringComparison.CurrentCultureIgnoreCase)
            || IsRiotClient(rule.ExecutablePath) && "Riot Client League of Legends LoL VALORANT Риот Лига легенд Валорант".Contains(query, StringComparison.CurrentCultureIgnoreCase);
    }
}
