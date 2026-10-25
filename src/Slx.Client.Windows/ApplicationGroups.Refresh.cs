using System.ComponentModel;
using Slx.Client.Core;
namespace Slx.Client.Windows;
public static partial class ApplicationGroups
{
    public static bool NeedsRefresh(AppRule rule) => IsGroup(rule.ExecutablePath);
    /// <summary>User-side repair only: service identity cannot resolve the current user's registered package.</summary>
    public static IReadOnlyList<AppRule> Refresh(IReadOnlyList<AppRule> rules, CancellationToken token = default) =>
        Refresh(rules, PackageExecutables, token);
    internal static IReadOnlyList<AppRule> Refresh(IReadOnlyList<AppRule> rules,
        Func<CancellationToken, IReadOnlyList<string>> resolve, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        if (!rules.Any(NeedsRefresh)) return rules;
        string? current;
        try
        {
            current = resolve(token).FirstOrDefault(path => PlanValidator.IsExecutablePath(path) && File.Exists(path));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or Win32Exception or ClientFault)
        { token.ThrowIfCancellationRequested(); return rules; }
        token.ThrowIfCancellationRequested();
        if (current is null) return rules; // Unresolved groups retain their exact intent for fail-closed validation.
        // Expand adds only currently existing exact package/helper paths after this stale-member replacement.
        return rules.Select(rule => NeedsRefresh(rule)
                ? rule with { ExecutablePath = current, Name = "Codex / ChatGPT" } : rule)
            .GroupBy(rule => rule.ExecutablePath, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First() with { Enabled = group.Any(rule => rule.Enabled) }).ToArray();
    }
}
