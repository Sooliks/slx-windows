using Slx.Client.Core;
using System.Security.Cryptography;
using System.Text;
namespace Slx.Client.Windows;
/// <summary>Repairs only removed Discord/Roblox version paths, retaining exact executable rules.</summary>
public static partial class VersionedApplicationPaths
{
    public static bool NeedsRefresh(AppRule rule) => TryVersionRoot(rule.ExecutablePath, out _, out _);
    public static IReadOnlyList<AppRule> Refresh(IReadOnlyList<AppRule> rules)
    {
        var result = new List<AppRule>();
        foreach (var rule in rules)
        {
            if (!TryVersionRoot(rule.ExecutablePath, out var root, out var pattern))
            { result.Add(rule); continue; }
            try
            {
                if (!Directory.Exists(root) || IsLink(root)) { result.Add(rule); continue; }
                var candidates = Directory.EnumerateDirectories(root, pattern).Take(64)
                    .Where(directory => !IsLink(directory))
                    .Select(directory => Path.Combine(directory, Path.GetFileName(rule.ExecutablePath)))
                    .Where(File.Exists).Where(path => !IsLink(path))
                    .OrderByDescending(File.GetLastWriteTimeUtc).ThenByDescending(path => path, StringComparer.OrdinalIgnoreCase)
                    .Take(20).ToArray();
                var primary = File.Exists(rule.ExecutablePath) ? rule : candidates.Length == 0 ? rule : rule with { ExecutablePath = candidates[0] };
                result.Add(primary);
                foreach (var path in candidates.Where(path => !path.Equals(primary.ExecutablePath, StringComparison.OrdinalIgnoreCase)))
                {
                    var id = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(path.ToUpperInvariant())).AsSpan(0, 16));
                    result.Add(rule with { Id = id, ExecutablePath = path });
                }
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            { result.Add(rule); } // Leave unresolved paths for the service's fail-closed validation.
        }
        return result.DistinctBy(rule => rule.ExecutablePath, StringComparer.OrdinalIgnoreCase).ToArray();
    }
    internal static bool TryVersionRoot(string path, out string root, out string pattern)
    {
        root = pattern = "";
        if (!Path.IsPathFullyQualified(path) || path.StartsWith(@"\\", StringComparison.Ordinal)) return false;
        var parent = Path.GetDirectoryName(path);
        if (parent is null) return false;
        var directory = Path.GetFileName(parent);
        var file = Path.GetFileName(path);
        var candidateRoot = Path.GetDirectoryName(parent);
        if (candidateRoot is null) return false;
        if (file.Equals("Discord.exe", StringComparison.OrdinalIgnoreCase)
            && directory.StartsWith("app-", StringComparison.OrdinalIgnoreCase)
            && Path.GetFileName(candidateRoot).Equals("Discord", StringComparison.OrdinalIgnoreCase)) pattern = "app-*";
        else if (file.Equals("RobloxPlayerBeta.exe", StringComparison.OrdinalIgnoreCase)
            && directory.StartsWith("version-", StringComparison.OrdinalIgnoreCase)
            && Path.GetFileName(candidateRoot).Equals("Versions", StringComparison.OrdinalIgnoreCase)
            && string.Equals(Path.GetFileName(Path.GetDirectoryName(candidateRoot)), "Roblox", StringComparison.OrdinalIgnoreCase)) pattern = "version-*";
        else return false;
        root = candidateRoot;
        return true;
    }
    private static bool IsLink(string path) => (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
}
