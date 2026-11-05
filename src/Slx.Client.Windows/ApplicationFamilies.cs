using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;
using Slx.Client.Core;
namespace Slx.Client.Windows;
/// <summary>
/// Applications whose network traffic comes from a different executable than the one users recognise:
/// VALORANT (game binary plus Riot Client) and the Claude Code CLI. Only exact, existing paths are added.
/// </summary>
public static class ApplicationFamilies
{
    internal const string Valorant = "VALORANT";
    internal const string ClaudeCode = "Claude Code";
    private const string ValorantGame = "VALORANT-Win64-Shipping.exe";
    private static string ClaudeCodeBin => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin", "claude.exe");

    public static IReadOnlyList<AppRule> Discover(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var found = new List<AppRule>();
        var game = ValorantRoots().Select(GamePath).FirstOrDefault(File.Exists);
        if (game is not null) found.Add(AppDiscovery.FromExecutable(game) with { Name = Valorant });
        cancellationToken.ThrowIfCancellationRequested();
        if (File.Exists(ClaudeCodeBin)) found.Add(AppDiscovery.FromExecutable(ClaudeCodeBin) with { Name = ClaudeCode });
        return found;
    }

    /// <summary>Members to add next to a selected rule; empty for ordinary applications.</summary>
    internal static IEnumerable<string> Members(string path)
    {
        string file = Path.GetFileName(path), directory = Path.GetDirectoryName(path) ?? "";
        string? live = file.Equals(ValorantGame, StringComparison.OrdinalIgnoreCase)
            ? Ancestor(directory, 3)
            : file.Equals("VALORANT.exe", StringComparison.OrdinalIgnoreCase) && Directory.Exists(Path.Combine(directory, "ShooterGame"))
                ? directory : null;
        if (live is null) return [];
        var members = new List<string> { GamePath(live), Path.Combine(live, "VALORANT.exe") };
        foreach (var client in RiotClients(live))
            foreach (var name in new[] { "RiotClientServices.exe", "Riot Client.exe", @"UX\RiotClientUx.exe", @"UX\RiotClientUxRender.exe" })
                members.Add(Path.Combine(client, name));
        return members.Where(member => PlanValidator.IsExecutablePath(member) && File.Exists(member) && !IsLink(member))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    /// <summary>A symlinked executable runs as its target; WFP and the core see only that final path.</summary>
    internal static string ResolveLink(string path)
    {
        try
        {
            if (!File.Exists(path) || !IsLink(path)) return path;
            var target = File.ResolveLinkTarget(path, returnFinalTarget: true);
            return target is FileInfo { Exists: true } file && !IsLink(file.FullName) && PlanValidator.IsExecutablePath(file.FullName)
                ? file.FullName : path;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException) { return path; }
    }

    internal static Guid Id(string path) => new(SHA256.HashData(Encoding.UTF8.GetBytes(path.ToUpperInvariant())).AsSpan(0, 16));
    private static string GamePath(string live) => Path.Combine(live, "ShooterGame", "Binaries", "Win64", ValorantGame);
    private static bool IsLink(string path) => (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
    private static string? Ancestor(string directory, int levels)
    {
        string? current = directory;
        for (int index = 0; index < levels && current is not null; index++) current = Path.GetDirectoryName(current);
        return string.IsNullOrEmpty(current) ? null : current;
    }

    private static IEnumerable<string> ValorantRoots()
    {
        var roots = new List<string>();
        foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
            foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                try
                {
                    using var registry = RegistryKey.OpenBaseKey(hive, view);
                    using var entry = registry.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Riot Game valorant.live");
                    if (entry?.GetValue("InstallLocation") is string location && location.Length is > 3 and < 1024) roots.Add(location.Trim('"'));
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException) { }
            }
        roots.AddRange(InstallsFile().Associated.Where(key => key.Contains("valorant", StringComparison.OrdinalIgnoreCase)));
        foreach (var drive in DriveLetters()) roots.Add(drive + @"Riot Games\VALORANT\live");
        return roots.Select(root => root.Replace('/', '\\').TrimEnd('\\')).Where(root => Path.IsPathFullyQualified(root))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> RiotClients(string live)
    {
        var clients = InstallsFile().Clients.Select(client => Path.GetDirectoryName(client)).OfType<string>().ToList();
        if (Ancestor(live, 2) is { } games) clients.Add(Path.Combine(games, "Riot Client"));
        foreach (var drive in DriveLetters()) clients.Add(drive + @"Riot Games\Riot Client");
        return clients.Select(client => client.Replace('/', '\\').TrimEnd('\\')).Where(root => Path.IsPathFullyQualified(root))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> DriveLetters()
    {
        try { return DriveInfo.GetDrives().Where(drive => drive.DriveType == DriveType.Fixed).Select(drive => drive.Name).Take(8).ToArray(); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { return [@"C:\"]; }
    }

    /// <summary>Riot's own install map: game folders and Riot Client paths. Size-bounded and read-only.</summary>
    private static (IReadOnlyList<string> Associated, IReadOnlyList<string> Clients) InstallsFile()
    {
        string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Riot Games", "RiotClientInstalls.json");
        try
        {
            if (!File.Exists(path) || new FileInfo(path).Length > 65536) return (Array.Empty<string>(), Array.Empty<string>());
            using var json = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions { MaxDepth = 8 });
            if (json.RootElement.ValueKind != JsonValueKind.Object) return (Array.Empty<string>(), Array.Empty<string>());
            var associated = new List<string>(); var clients = new List<string>();
            if (json.RootElement.TryGetProperty("associated_client", out var map) && map.ValueKind == JsonValueKind.Object)
                foreach (var item in map.EnumerateObject().Take(32))
                {
                    associated.Add(item.Name);
                    if (item.Value.ValueKind == JsonValueKind.String) clients.Add(item.Value.GetString()!);
                }
            foreach (var key in new[] { "rc_default", "rc_live" })
                if (json.RootElement.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String) clients.Add(value.GetString()!);
            return (associated.Where(item => item.Length < 1024).ToArray(), clients.Where(item => item.Length < 1024).ToArray());
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        { return (Array.Empty<string>(), Array.Empty<string>()); }
    }
}
