using Slx.Client.Core;
using Slx.Client.Service;
using Slx.Client.Windows;
using Xunit;
namespace Slx.Client.Tests;
/// <summary>Owned local junction fixtures only. No shell, service, packets or remote filesystem targets.</summary>
public sealed class ProtectedVersionedApplicationPathsTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "slx-protected-path-" + Guid.NewGuid().ToString("N"));
    [Fact]
    public void AncestorJunctionCannotBeUsedAsARecoverySearchRoot()
    {
        string actual = Create(@"Actual\Discord\app-new");
        string alias = Path.Combine(root, "Alias");
        using var junction = LocalJunctionFixture.Create(alias, Path.Combine(root, "Actual"));
        var rule = CoreFixtures.App(Path.Combine(alias, "Discord", "app-old", "Discord.exe"));
        Assert.Equal(rule, Assert.Single(VersionedApplicationPaths.RefreshProtected([rule], default)));
        var plan = CoreFixtures.Plan() with { Applications = [rule] };
        Assert.Equal(ExactExecutablePath.Unsupported,
            Assert.Throws<ClientFault>(() => RecoveredApplicationPlan.Prepare(plan, default)).Code);
        Assert.True(File.Exists(actual));
    }
    [Fact]
    public void ReparseCandidateCannotHideAnotherValidLocalVersionOrBecomeASelectedPath()
    {
        string actual = Create(@"Discord\app-valid");
        string outside = Create("Outside");
        string link = Path.Combine(root, "Discord", "app-junction");
        using var junction = LocalJunctionFixture.Create(link, Path.GetDirectoryName(outside)!);
        var rule = CoreFixtures.App(Path.Combine(root, "Discord", "app-old", "Discord.exe"));
        var refreshed = Assert.Single(VersionedApplicationPaths.RefreshProtected([rule], default));
        Assert.Equal(actual, refreshed.ExecutablePath); Assert.Equal(rule.Id, refreshed.Id);
        var prepared = RecoveredApplicationPlan.Prepare(CoreFixtures.Plan() with { Applications = [rule] }, default);
        Assert.Equal(actual, Assert.Single(prepared.Applications).ExecutablePath);
        Assert.True(File.Exists(outside));
    }
    [Fact]
    public void DirectoryLeaseHoldsAncestorsAgainstRenameAndReleasesThemOnDispose()
    {
        _ = Create(@"Lease\Discord\app-current");
        string ancestor = Path.Combine(root, "Lease"); string moved = Path.Combine(root, "Moved");
        using (var lease = ExactExecutablePath.OpenDirectory(Path.Combine(ancestor, "Discord"), default))
        {
            Assert.Throws<IOException>(() => Directory.Move(ancestor, moved));
            Assert.Equal(Path.Combine(ancestor, "Discord"), lease.Path);
        }
        Directory.Move(ancestor, moved); Assert.True(Directory.Exists(moved));
    }
    [Fact]
    public void MissingAndDisabledVersionedRowsKeepTheirExactIntentWithoutExpansion()
    {
        _ = Create(@"Discord\app-new");
        var disabled = CoreFixtures.App(Path.Combine(root, "Discord", "app-old", "Discord.exe"), false);
        var missing = CoreFixtures.App(Path.Combine(root, "Roblox", "Versions", "version-old", "RobloxPlayerBeta.exe"));
        var result = VersionedApplicationPaths.RefreshProtected([disabled, missing], default);
        Assert.Equal(new[] { disabled, missing }, result);
    }
    private string Create(string directory)
    {
        string folder = Path.Combine(root, directory); Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "Discord.exe"); File.Copy(Environment.ProcessPath!, path); return path;
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
