using System.ComponentModel;
using Slx.Client.Core;
using Slx.Client.Windows;
using Xunit;
namespace Slx.Client.Tests;
/// <summary>Injected package discovery and temporary files only; no registered package, preference or service changes.</summary>
public sealed class ApplicationGroupRefreshTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "slx-package-refresh-" + Guid.NewGuid().ToString("N"));
    private static string RemovedPackage => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        "WindowsApps", "OpenAI.Codex_removed-fixture", "app", "ChatGPT.exe");
    private static string RemovedHelper => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OpenAI", "Codex", "bin", "removed-fixture", "CODEX.EXE");
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CurrentPackageReplacesRemovedExactPathAndPreservesUserChoice(bool enabled)
    {
        var current = Create("current", "ChatGPT.exe");
        var original = new AppRule(Guid.NewGuid(), "Codex / ChatGPT", RemovedPackage, enabled);
        var refreshed = Assert.Single(ApplicationGroups.Refresh([original], _ => [current]));
        Assert.Equal(original.Id, refreshed.Id); Assert.Equal(enabled, refreshed.Enabled);
        Assert.Equal(original.Name, refreshed.Name); Assert.Equal(current, refreshed.ExecutablePath);
        Assert.Equal(RemovedPackage, original.ExecutablePath);
        if (OperatingSystem.IsWindows()) Assert.NotEmpty(TrafficApplicationIds.Read(refreshed.ExecutablePath, true));
    }
    [Fact]
    public void SuccessfulGroupRefreshDropsStaleOptionalMembersAndDoesNotTouchOtherApps()
    {
        var current = Create("current", "ChatGPT.exe");
        var primary = new AppRule(Guid.NewGuid(), "Codex / ChatGPT", RemovedPackage);
        var helper = new AppRule(Guid.NewGuid(), primary.Name, RemovedHelper);
        var ordinary = new AppRule(Guid.NewGuid(), "Ordinary app", Create("other", "other.exe"), false);
        var refreshed = ApplicationGroups.Refresh([primary, helper, ordinary], _ => [current]);
        Assert.Equal(2, refreshed.Count); Assert.Equal(primary.Id, refreshed[0].Id);
        Assert.Equal(current, refreshed[0].ExecutablePath); Assert.Equal(ordinary, refreshed[1]);
        Assert.DoesNotContain(refreshed, rule => rule.ExecutablePath == RemovedPackage || rule.ExecutablePath == RemovedHelper);
        PlanValidator.Validate(CoreFixtures.Plan() with { Applications = refreshed.ToList() });
    }
    [Fact]
    public void GroupMergeUsesFirstIdentityAndRetainsAnEnabledMember()
    {
        var current = Create("current", "ChatGPT.exe");
        var primary = new AppRule(Guid.NewGuid(), "Codex / ChatGPT", RemovedPackage, false);
        var helper = new AppRule(Guid.NewGuid(), primary.Name, RemovedHelper, true);
        var refreshed = Assert.Single(ApplicationGroups.Refresh([primary, helper], _ => [current]));
        Assert.Equal(primary.Id, refreshed.Id); Assert.True(refreshed.Enabled);
    }
    [Fact]
    public void MissingOrInvalidAuthoritativeCandidateDoesNotEraseUnresolvedIntent()
    {
        AppRule[] original = [new(Guid.NewGuid(), "Codex / ChatGPT", RemovedPackage, false),
            new(Guid.NewGuid(), "Codex / ChatGPT", RemovedHelper, false)];
        foreach (var candidates in new IReadOnlyList<string>[] { [], [Path.Combine(root, "not-installed.exe")], [Create("current", "NotAnExecutable.dll")] })
            Assert.Same(original, ApplicationGroups.Refresh(original, _ => candidates));
    }
    [Theory]
    [InlineData("io")]
    [InlineData("denied")]
    [InlineData("native")]
    [InlineData("client")]
    public void ExpectedResolverFailurePreservesExactIntent(string failure)
    {
        AppRule[] original = [new(Guid.NewGuid(), "Codex / ChatGPT", RemovedPackage, false)];
        Exception problem = failure switch
        {
            "io" => new IOException(), "denied" => new UnauthorizedAccessException(),
            "native" => new Win32Exception(5), _ => new ClientFault("PACKAGE_UNAVAILABLE")
        };
        var refreshed = ApplicationGroups.Refresh(original, _ => throw problem);
        Assert.Same(original, refreshed);
    }
    [Fact]
    public void CancellationBeforeAndAfterResolutionNeverAppliesAPartialRepair()
    {
        var current = Create("current", "ChatGPT.exe");
        AppRule[] original = [new(Guid.NewGuid(), "Codex / ChatGPT", RemovedPackage)];
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        Assert.Throws<OperationCanceledException>(() => ApplicationGroups.Refresh(original, _ => throw new InvalidOperationException(), cancelled.Token));
        using var during = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => ApplicationGroups.Refresh(original, token =>
        { Assert.Equal(during.Token, token); during.Cancel(); return [current]; }, during.Token));
        Assert.Equal(RemovedPackage, Assert.Single(original).ExecutablePath);
    }
    [Fact]
    public void OrdinaryExecutableDoesNotInvokePackageDiscovery()
    {
        AppRule[] original = [new(Guid.NewGuid(), "Ordinary", Create("ordinary", "app.exe"))];
        Assert.Same(original, ApplicationGroups.Refresh(original, _ => throw new InvalidOperationException()));
        Assert.False(ApplicationGroups.NeedsRefresh(original[0]));
    }
    private string Create(string directory, string name)
    {
        var folder = Path.Combine(root, directory); Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, name); File.WriteAllBytes(path, [0]); return path;
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
