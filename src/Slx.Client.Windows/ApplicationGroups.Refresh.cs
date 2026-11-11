using System.ComponentModel;
using Slx.Client.Core;
namespace Slx.Client.Windows;
public static partial class ApplicationGroups
{
    public static bool NeedsRefresh(AppRule rule) => IsGroup(rule.ExecutablePath) || IsClaude(rule);
    public static bool IsClaude(AppRule rule) => ClaudeApplicationGroup.Current.Identify(rule.ExecutablePath) is not null;
    public static string GroupKey(AppRule rule) => IsClaude(rule) ? "family:claude" : "label:" + rule.Name.Trim();
    public static IReadOnlyList<AppRule> Restore(IReadOnlyList<AppRule> rules) =>
        ClaudeApplicationGroup.Current.Migrate(rules).Select(Normalize).ToArray();
    public static IReadOnlyList<AppRule> RefreshClaude(IReadOnlyList<AppRule> rules, CancellationToken token = default) =>
        ClaudeApplicationGroup.Current.Refresh(rules, token);
    public static System.IO.FileSystemWatcher? WatchClaudeCode(Action changed) => ClaudeApplicationGroup.Current.WatchCode(changed);
    public static System.IO.FileSystemWatcher? WatchClaudeDesktop(Action changed) => ClaudeApplicationGroup.Current.WatchDesktop(changed);
    public static string MemberLabel(string path) => ClaudeApplicationGroup.Current.Identify(path) switch
    { ClaudeApplicationGroup.Member.Desktop => "Claude Desktop", ClaudeApplicationGroup.Member.Code => "Claude Code", _ => Path.GetFileName(path) };
    /// <summary>User-side repair only: service identity cannot resolve the current user's registered package.</summary>
    public static IReadOnlyList<AppRule> Refresh(IReadOnlyList<AppRule> rules, CancellationToken token = default) =>
        RefreshClaude(Refresh(rules, PackageExecutables, token), token);
    internal static IReadOnlyList<AppRule> Refresh(IReadOnlyList<AppRule> rules,
        Func<CancellationToken, IReadOnlyList<string>> resolve, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        if (!rules.Any(rule => IsGroup(rule.ExecutablePath))) return rules;
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
        return rules.Select(rule => IsGroup(rule.ExecutablePath)
                ? rule with { ExecutablePath = current, Name = "Codex / ChatGPT" } : rule)
            .GroupBy(rule => rule.ExecutablePath, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First() with { Enabled = group.Any(rule => rule.Enabled) }).ToArray();
    }
}
