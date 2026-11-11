using Slx.Client.Core;

namespace Slx.Client.Windows;

/// <summary>The current user's Anthropic install layouts; labels and arbitrary executable names grant no membership.</summary>
internal sealed class ClaudeApplicationGroup(string local, string roaming)
{
    internal const string Name = "Claude";
    internal static ClaudeApplicationGroup Current { get; } = new(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
    private readonly string desktopRoot = Path.Combine(local, "AnthropicClaude");
    private readonly string codeRoot = Path.Combine(roaming, "Claude", "claude-code");
    private const int DirectoryBudget = 64, VersionsPerRoot = 20;

    internal enum Member { Desktop, Code, Helper }

    internal Member? Identify(string path)
    {
        if (!PlanValidator.IsExecutablePath(path)) return null;
        path = path.Replace('/', '\\');
        if (!Path.GetFullPath(path).Equals(path, StringComparison.OrdinalIgnoreCase)) return null;
        if (Under(path, desktopRoot) is { } desktop)
        {
            if (Same(desktop, "claude.exe")) return Member.Desktop;
            if (Same(desktop, "Update.exe")) return Member.Helper;
            string[] parts = desktop.Split('\\');
            if (parts.Length < 2 || DesktopVersion(parts[0]) is null) return null;
            if (parts.Length == 2 && Same(parts[1], "claude.exe")) return Member.Desktop;
            if (parts.Length == 2 && Same(parts[1], "squirrel.exe")
                || parts.Length == 3 && Same(parts[1], "resources") && Same(parts[2], "chrome-native-host.exe"))
                return Member.Helper;
        }
        if (Under(path, codeRoot) is { } code)
        {
            if (Same(Path.GetFileName(code), "claude.exe") && Path.GetDirectoryName(code) is { } parent
                && IsCodeDirectory(parent)) return Member.Code;
        }
        return null;
    }

    internal IReadOnlyList<AppRule> Migrate(IReadOnlyList<AppRule> rules) => Replace(rules, null);

    internal IReadOnlyList<AppRule> Refresh(IReadOnlyList<AppRule> rules, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        if (!rules.Any(rule => Identify(rule.ExecutablePath) is not null)) return rules;
        var current = Discover(token);
        token.ThrowIfCancellationRequested();
        return Replace(rules, current.Count == 0 ? null : current);
    }

    internal IReadOnlyList<string> Discover(CancellationToken token)
    {
        var paths = new List<string>();
        AddVersions(desktopRoot, DesktopVersion, ["claude.exe", "squirrel.exe", @"resources\chrome-native-host.exe"], paths, token);
        AddVersions(codeRoot, ParseVersion, ["claude.exe"], paths, token, includeCodeBuilds: true);
        AddExisting(Path.Combine(desktopRoot, "claude.exe"), paths, token);
        AddExisting(Path.Combine(desktopRoot, "Update.exe"), paths, token);
        return paths.OrderBy(Identify).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private IReadOnlyList<AppRule> Replace(IReadOnlyList<AppRule> rules, IReadOnlyList<string>? current)
    {
        var members = rules.Where(rule => Identify(rule.ExecutablePath) is not null).ToArray();
        if (members.Length == 0) return rules;
        var desktop = members.Where(rule => Identify(rule.ExecutablePath) == Member.Desktop).ToArray();
        var primary = desktop.FirstOrDefault() ?? members[0];
        // The saved Desktop switch wins over a manually added Code workaround. Code-only selections keep their choice.
        bool enabled = (desktop.Length == 0 ? members : desktop).Any(rule => rule.Enabled);
        var selected = current ?? members.OrderBy(rule => Identify(rule.ExecutablePath)).Select(rule => rule.ExecutablePath)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var replacements = selected.Select((path, index) =>
        {
            var existing = members.FirstOrDefault(rule => Same(rule.ExecutablePath.Replace('/', '\\'), path));
            Guid id = index == 0 ? primary.Id : existing is not null && existing.Id != primary.Id
                ? existing.Id : ApplicationFamilies.Id(path);
            return new AppRule(id, Name, path, enabled);
        });
        var result = new List<AppRule>();
        bool inserted = false;
        foreach (var rule in rules)
        {
            if (Identify(rule.ExecutablePath) is null) result.Add(rule);
            else if (!inserted) { result.AddRange(replacements); inserted = true; }
        }
        return result;
    }

    private void AddVersions(string root, Func<string, Version?> version, string[] names, List<string> result,
        CancellationToken token, bool includeCodeBuilds = false)
    {
        token.ThrowIfCancellationRequested();
        try
        {
            using var directory = ExactExecutablePath.OpenDirectory(root, token);
            var candidates = Directory.EnumerateDirectories(directory.Path).Take(DirectoryBudget)
                .Select(path => (Path: path, Version: version(Path.GetFileName(path))))
                .Where(item => item.Version is not null)
                .OrderByDescending(item => item.Version).ThenByDescending(item => item.Path, StringComparer.OrdinalIgnoreCase);
            int accepted = 0;
            int pathLimit = includeCodeBuilds ? result.Count + VersionsPerRoot : int.MaxValue;
            foreach (var item in candidates)
            {
                int before = result.Count;
                foreach (string name in names) AddExisting(Path.Combine(item.Path, name), result, token);
                if (includeCodeBuilds) AddCodeBuilds(item.Path, result, pathLimit, token);
                if (result.Count >= pathLimit || (result.Count != before && ++accepted == VersionsPerRoot)) break;
            }
        }
        catch (Exception error) when (Unavailable(error)) { }
    }

    private void AddCodeBuilds(string version, List<string> result, int limit, CancellationToken token)
    {
        if (result.Count >= limit) return;
        try
        {
            using var directory = ExactExecutablePath.OpenDirectory(version, token);
            var builds = Directory.EnumerateDirectories(directory.Path).Take(DirectoryBudget)
                .Where(path => IsCodeBuild(Path.GetFileName(path))).Order(StringComparer.OrdinalIgnoreCase);
            foreach (string build in builds)
            {
                AddExisting(Path.Combine(build, "claude.exe"), result, token);
                if (result.Count >= limit) break;
            }
        }
        catch (Exception error) when (Unavailable(error)) { }
    }

    private void AddExisting(string path, List<string> result, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (Identify(path) is null) return;
        try { using var file = ExactExecutablePath.Open(path, token); result.Add(file.Path); }
        catch (Exception error) when (Unavailable(error)) { }
    }

    internal FileSystemWatcher? WatchCode(Action changed) => Watch(Path.GetDirectoryName(codeRoot)!,
        path => Same(path, codeRoot) || Identify(path) == Member.Code
            || Under(path, codeRoot) is { } child && IsCodeDirectory(child), changed);

    internal FileSystemWatcher? WatchDesktop(Action changed) => Watch(desktopRoot,
        path => Identify(path) is not null || Under(path, desktopRoot) is { } child && DesktopVersion(child) is not null, changed);

    private static FileSystemWatcher? Watch(string root, Func<string, bool> relevant, Action changed)
    {
        FileSystemWatcher? watcher = null;
        try
        {
            using var directory = ExactExecutablePath.OpenDirectory(root, default);
            watcher = new(directory.Path) { IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName };
            watcher.Created += (_, args) => { if (relevant(args.FullPath)) changed(); };
            watcher.Deleted += (_, args) => { if (relevant(args.FullPath)) changed(); };
            watcher.Renamed += (_, args) => { if (relevant(args.FullPath) || relevant(args.OldFullPath)) changed(); };
            watcher.EnableRaisingEvents = true;
            return watcher;
        }
        catch (Exception error) when (Unavailable(error)) { watcher?.Dispose(); return null; }
    }

    private static string? Under(string path, string root) =>
        path.StartsWith(root + "\\", StringComparison.OrdinalIgnoreCase) ? path[(root.Length + 1)..] : null;
    private static bool Same(string a, string b) => a.Equals(b, StringComparison.OrdinalIgnoreCase);
    private static Version? DesktopVersion(string name) => name.StartsWith("app-", StringComparison.OrdinalIgnoreCase)
        ? ParseVersion(name[4..]) : null;
    private static bool IsCodeDirectory(string path)
    {
        var parts = path.Split('\\');
        return ParseVersion(parts[0]) is not null && (parts.Length == 1 || parts.Length == 2 && IsCodeBuild(parts[1]));
    }
    private static bool IsCodeBuild(string name) => name.Length == 12 && name.All(char.IsAsciiHexDigit);
    private static Version? ParseVersion(string name)
    {
        if (name.Length is 0 or >64 || name.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('.' or '-'))) return null;
        return Version.TryParse(name.Split('-', 2)[0], out var parsed) ? parsed : null;
    }
    private static bool Unavailable(Exception error) => error is IOException or UnauthorizedAccessException or ClientFault;
}
