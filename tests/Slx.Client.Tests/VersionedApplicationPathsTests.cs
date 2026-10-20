using Slx.Client.Core;
using Slx.Client.Windows;
using Xunit;
namespace Slx.Client.Tests;
public sealed class VersionedApplicationPathsTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "slx-path-fixture-" + Guid.NewGuid().ToString("N"));
    [Theory]
    [InlineData("Discord", "app-old", "app-new", "Discord.exe")]
    [InlineData(@"Roblox\Versions", "version-old", "version-new", "RobloxPlayerBeta.exe")]
    public void RemovedVersionUsesExistingSiblingAndPreservesRule(string app, string oldVersion, string newVersion, string executable)
    {
        var previous = Path.Combine(root, app, oldVersion, executable);
        var current = Create(app, newVersion, executable);
        var rule = new AppRule(Guid.NewGuid(), "Chosen app", previous, false);
        Assert.True(VersionedApplicationPaths.NeedsRefresh(rule));
        var result = Assert.Single(VersionedApplicationPaths.Refresh([rule]));
        Assert.Equal(current, result.ExecutablePath);
        Assert.Equal(rule.Id, result.Id); Assert.Equal(rule.Name, result.Name); Assert.False(result.Enabled);
    }
    [Fact]
    public void ExistingPathRemainsProtectedAndNewSiblingAlsoGetsAnExactRule()
    {
        var existing = Create("Discord", "app-old", "Discord.exe");
        var newer = Create("Discord", "app-new", "Discord.exe");
        var rule = new AppRule(Guid.NewGuid(), "Discord", existing);
        var refreshed = VersionedApplicationPaths.Refresh([rule]);
        Assert.Equal(2, refreshed.Count); Assert.Contains(rule, refreshed);
        Assert.Contains(refreshed, item => item.ExecutablePath == newer && item.Enabled);
        Assert.Equal(2, refreshed.Select(item => item.Id).Distinct().Count());
        var missing = rule with { ExecutablePath = Path.Combine(root, "Other", "Missing.exe") };
        Assert.Equal(missing, Assert.Single(VersionedApplicationPaths.Refresh([missing])));
    }
    [Fact]
    public void OtherRootsAndOtherExecutablesDoNotBecomeReplacementCandidates()
    {
        _ = Create("Discord", "app-new", "Discord.exe");
        foreach (var path in new[] { Path.Combine(root, "Unrelated", "app-old", "Discord.exe"),
            Path.Combine(root, "Discord", "app-old", "Helper.exe"), @"\\server\Discord\app-old\Discord.exe" })
        {
            var rule = new AppRule(Guid.NewGuid(), "Discord", path);
            Assert.False(VersionedApplicationPaths.NeedsRefresh(rule));
            Assert.Equal(rule, Assert.Single(VersionedApplicationPaths.Refresh([rule])));
        }
    }
    private string Create(string app, string version, string executable)
    {
        var folder = Path.Combine(root, app, version); Directory.CreateDirectory(folder);
        var file = Path.Combine(folder, executable); File.WriteAllText(file, "local path fixture, never executed"); return file;
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
