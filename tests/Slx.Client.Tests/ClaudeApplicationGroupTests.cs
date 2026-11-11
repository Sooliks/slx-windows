using Slx.Client.Core;
using Slx.Client.Windows;
using Xunit;

namespace Slx.Client.Tests;

public sealed class ClaudeApplicationGroupTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "slx-claude-" + Guid.NewGuid().ToString("N"));
    private ClaudeApplicationGroup Family => new(Path.Combine(root, "Other Local"), Path.Combine(root, "Other Roaming"));
    private string Desktop(string version) => Path.Combine(root, "Other Local", "AnthropicClaude", "app-" + version, "claude.exe");
    private string Code(string version) => Path.Combine(root, "Other Roaming", "Claude", "claude-code", version, "claude.exe");
    private static AppRule Rule(string path, string name = "Claude", bool enabled = true) => new(Guid.NewGuid(), name, path, enabled);

    [Fact]
    public async Task FirstCodeLaunchAfterSelectionIsObservedAndExpandsTheExistingRule()
    {
        string desktop = Create(Desktop("1.20186.9"));
        var family = Family;
        var selected = Rule(desktop);
        Directory.CreateDirectory(Path.Combine(root, "Other Roaming", "Claude"));
        Assert.Equal(selected, Assert.Single(family.Refresh([selected])));
        string code = Code("2.1.209");
        var appeared = new TaskCompletionSource<IReadOnlyList<AppRule>>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var watcher = family.WatchCode(() =>
        {
            var refreshed = family.Refresh([selected]);
            if (refreshed.Any(rule => rule.ExecutablePath == code)) appeared.TrySetResult(refreshed);
        });
        Assert.NotNull(watcher);
        Create(code);
        var applied = await appeared.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(new[] { desktop, code }, applied.Select(rule => rule.ExecutablePath));
        Assert.All(applied, rule => Assert.True(rule.Enabled));
        Assert.Equal(selected.Id, applied[0].Id);
    }

    [Fact]
    public void VersionChangeReplacesDeletedPathsAndKeepsStillInstalledVersionsExact()
    {
        var family = Family;
        string desktop = Create(Desktop("1.2.3")), oldCode = Create(Code("2.1.9"));
        var selected = family.Refresh([Rule(desktop)]);
        string nextCode = Create(Code("2.1.10"));
        var both = family.Refresh(selected);
        Assert.Equal(new[] { desktop, nextCode, oldCode }, both.Select(rule => rule.ExecutablePath));
        File.Delete(oldCode);
        string nextDesktop = Create(Desktop("1.2.4"));
        File.Delete(desktop);
        var refreshed = family.Refresh(both);
        Assert.Equal(new[] { nextDesktop, nextCode }, refreshed.Select(rule => rule.ExecutablePath));
        Assert.Equal(selected[0].Id, refreshed[0].Id);
        Assert.All(refreshed, rule => Assert.True(rule.Enabled));
    }

    [Fact]
    public async Task DesktopUpgradeIsDiscoveredWhileTheExistingSelectionStaysOn()
    {
        var family = Family;
        string previous = Create(Desktop("1.20186.9"));
        string updated = Desktop("5.1231.0");
        var selected = Rule(previous);
        var appeared = new TaskCompletionSource<IReadOnlyList<AppRule>>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var watcher = family.WatchDesktop(() =>
        {
            var current = family.Refresh([selected]);
            if (current.Any(rule => rule.ExecutablePath == updated)) appeared.TrySetResult(current);
        });
        Assert.NotNull(watcher);
        Create(updated);
        var rules = await appeared.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(new[] { updated, previous }, rules.Select(rule => rule.ExecutablePath));
        Assert.Equal(selected.Id, rules[0].Id);
        Assert.All(rules, rule => Assert.True(rule.Enabled));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void SavedDesktopChoiceWinsOverManualCodeDuplicate(bool desktopEnabled, bool codeEnabled)
    {
        var desktop = Rule(Create(Desktop("1.2.3")), "Claude Desktop", desktopEnabled);
        var code = Rule(Create(Code("2.1.209")), "Claude Code", codeEnabled);
        var duplicate = Rule(code.ExecutablePath.ToUpperInvariant(), "Manual Claude", codeEnabled);
        var foreign = Rule(Create(Path.Combine(root, "Unrelated", "claude.exe")), "Claude", true);
        var migrated = Family.Migrate([code, duplicate, foreign, desktop]);
        Assert.Equal(3, migrated.Count);
        Assert.Equal(desktop.Id, migrated[0].Id);
        Assert.Equal(desktop.ExecutablePath, migrated[0].ExecutablePath);
        Assert.Equal(foreign, migrated[2]);
        Assert.All(migrated.Take(2), rule => { Assert.Equal("Claude", rule.Name); Assert.Equal(desktopEnabled, rule.Enabled); });
        Assert.Equal(migrated, Family.Refresh(migrated));
    }

    [Fact]
    public void CodeOnlySavedChoiceKeepsIdentityAndExpandsOnlyToRecognizedExistingHelpers()
    {
        string desktop = Create(Desktop("1.2.3"));
        string code = Create(Code("2.1.209"));
        string version = Path.GetDirectoryName(desktop)!;
        string host = Create(Path.Combine(version, "resources", "chrome-native-host.exe"));
        string updater = Create(Path.Combine(version, "squirrel.exe"));
        string foreign = Create(Path.Combine(version, "resources", "shell.exe"));
        var original = Rule(code, "Manual Code");
        var rules = Family.Refresh([original]);
        Assert.Equal(original.Id, rules[0].Id);
        Assert.Equal(desktop, rules[0].ExecutablePath);
        Assert.Equal(4, rules.Select(rule => rule.Id).Distinct().Count());
        Assert.Contains(rules, rule => rule.ExecutablePath == host);
        Assert.Contains(rules, rule => rule.ExecutablePath == updater);
        Assert.DoesNotContain(rules, rule => rule.ExecutablePath == foreign);
    }

    [Fact]
    public void SimilarRootsNonVersionFoldersAndDirectoryJunctionsDoNotBecomeRules()
    {
        string desktop = Create(Desktop("1.2.3"));
        _ = Create(Code("unrecognized"));
        _ = Create(Path.Combine(root, "Other Roaming", "Claude", "claude-code-backup", "2.1.1", "claude.exe"));
        string outside = Create(Path.Combine(root, "Outside", "claude.exe"));
        string alias = Path.GetDirectoryName(Code("2.1.209"))!;
        Directory.CreateDirectory(Path.GetDirectoryName(alias)!);
        using var junction = LocalJunctionFixture.Create(alias, Path.GetDirectoryName(outside)!);
        Assert.Equal(desktop, Assert.Single(Family.Refresh([Rule(desktop)])).ExecutablePath);
    }

    [Fact]
    public void CancelledAndMissingResolutionsDoNotInventAnExecutable()
    {
        var rule = Rule(Desktop("1.2.3"));
        Assert.Equal(rule, Assert.Single(Family.Refresh([rule])));
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        Assert.Throws<OperationCanceledException>(() => Family.Refresh([rule], cancelled.Token));
    }

    [Fact]
    public void RootLauncherAndUpdaterBelongToTheSameGroup()
    {
        string install = Path.Combine(root, "Other Local", "AnthropicClaude");
        string launcher = Create(Path.Combine(install, "claude.exe"));
        string update = Create(Path.Combine(install, "Update.exe"));
        string desktop = Create(Desktop("1.2.3"));
        string code = Create(Code("2.1.209"));
        var rule = Rule(launcher);
        var expanded = Family.Refresh([rule]);
        Assert.Equal(new[] { desktop, launcher, code, update }, expanded.Select(path => path.ExecutablePath));
        Assert.Equal(rule.Id, expanded[0].Id);
    }

    [Fact]
    public void VersionDiscoveryKeepsTheNewestTwentyExistingVersions()
    {
        string desktop = Create(Desktop("1.2.3"));
        for (int patch = 1; patch <= 26; patch++) _ = Create(Code("2.1." + patch));
        var rules = Family.Refresh([Rule(desktop)]);
        Assert.Equal(21, rules.Count);
        Assert.Equal(Code("2.1.26"), rules[1].ExecutablePath);
        Assert.Equal(Code("2.1.7"), rules[^1].ExecutablePath);
    }

    private static string Create(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, [0]);
        return path;
    }

    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
