using Slx.Client.Core;
using System.Security.Cryptography;
using System.Text;
namespace Slx.Client.Windows;
public static partial class VersionedApplicationPaths
{
    /// <summary>Privileged recovery reads only held local directories/EXEs, never reparse descendants.</summary>
    public static IReadOnlyList<AppRule> RefreshProtected(IReadOnlyList<AppRule> rules, CancellationToken token)
    {
        var explicitPaths = rules.Select(rule => rule.ExecutablePath.Replace('/', '\\'))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var result = new List<AppRule>();
        foreach (var rule in rules)
        {
            token.ThrowIfCancellationRequested();
            if (!rule.Enabled || !TryVersionRoot(rule.ExecutablePath, out var root, out var pattern))
            { result.Add(rule); continue; }
            try
            {
                using var directory = ExactExecutablePath.OpenDirectory(root, token);
                var candidates = new List<(string Path, DateTime Written)>();
                foreach (var version in Directory.EnumerateDirectories(directory.Path, pattern).Take(64))
                {
                    token.ThrowIfCancellationRequested();
                    string path = Path.Combine(version, Path.GetFileName(rule.ExecutablePath));
                    // Preserve an explicitly disabled/existing sibling's own rule, rather than broadening it.
                    if (!path.Equals(rule.ExecutablePath, StringComparison.OrdinalIgnoreCase)
                        && explicitPaths.Contains(path)) continue;
                    try
                    {
                        using var executable = ExactExecutablePath.Open(path, token);
                        candidates.Add((executable.Path, File.GetLastWriteTimeUtc(executable.Path)));
                    }
                    catch (Exception error) when (Unavailable(error)) { }
                }
                token.ThrowIfCancellationRequested();
                var paths = candidates.OrderByDescending(item => item.Written)
                    .ThenByDescending(item => item.Path, StringComparer.OrdinalIgnoreCase).Take(20)
                    .Select(item => item.Path).ToArray();
                bool exists;
                try { using var previous = ExactExecutablePath.Open(rule.ExecutablePath, token); exists = true; }
                catch (Exception error) when (Unavailable(error)) { exists = false; }
                var primary = exists || paths.Length == 0 ? rule : rule with { ExecutablePath = paths[0] };
                result.Add(primary);
                foreach (var path in paths.Where(path => !path.Equals(primary.ExecutablePath, StringComparison.OrdinalIgnoreCase)))
                {
                    var id = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(path.ToUpperInvariant())).AsSpan(0, 16));
                    result.Add(rule with { Id = id, ExecutablePath = path });
                }
            }
            catch (Exception error) when (Unavailable(error)) { result.Add(rule); }
        }
        token.ThrowIfCancellationRequested();
        return result.DistinctBy(rule => rule.ExecutablePath.Replace('/', '\\'), StringComparer.OrdinalIgnoreCase).ToArray();
    }
    private static bool Unavailable(Exception error) => error is IOException or UnauthorizedAccessException
        or ClientFault;
}
