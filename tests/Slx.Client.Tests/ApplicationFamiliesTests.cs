using Slx.Client.Core;
using Slx.Client.Windows;
using Xunit;

namespace Slx.Client.Tests;

public sealed class ApplicationFamiliesTests
{
    [Fact]
    public void ValorantLauncherOrGameSelectionRoutesTheActualGameBinaryToo()
    {
        string root = Path.Combine(Path.GetTempPath(), "slx-family-" + Guid.NewGuid().ToString("N"));
        string live = Path.Combine(root, "VALORANT", "live");
        string game = Path.Combine(live, "ShooterGame", "Binaries", "Win64", "VALORANT-Win64-Shipping.exe");
        string launcher = Path.Combine(live, "VALORANT.exe");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(game)!);
            File.WriteAllBytes(game, [0]); File.WriteAllBytes(launcher, [0]);
            foreach (var selected in new[] { game, launcher })
            {
                var members = ApplicationFamilies.Members(selected).ToArray();
                Assert.Contains(game, members, StringComparer.OrdinalIgnoreCase);
                Assert.Contains(launcher, members, StringComparer.OrdinalIgnoreCase);
                Assert.All(members, member => Assert.True(File.Exists(member)));
                var expanded = ApplicationGroups.Expand([new(Guid.NewGuid(), "VALORANT", selected)]);
                Assert.Contains(expanded, rule => rule.ExecutablePath.Equals(game, StringComparison.OrdinalIgnoreCase)
                    && rule.Enabled && rule.Name == "VALORANT");
                Assert.Equal(expanded.Count, expanded.Select(rule => rule.ExecutablePath.ToUpperInvariant()).Distinct().Count());
            }
            Assert.Empty(ApplicationFamilies.Members(Path.Combine(root, "VALORANT.exe")));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void OrdinaryOrMissingExecutablesGainNoMembersAndKeepTheirPath()
    {
        const string missing = @"C:\SLX Missing\VALORANT-Win64-Shipping.exe";
        Assert.All(ApplicationFamilies.Members(missing), member => Assert.True(File.Exists(member)));
        Assert.Empty(ApplicationFamilies.Members(@"C:\Apps\Browser\browser.exe"));
        Assert.Equal(missing, ApplicationFamilies.ResolveLink(missing));
        var rule = new AppRule(Guid.NewGuid(), "Browser", @"C:\Apps\Browser\browser.exe");
        Assert.Equal(rule, Assert.Single(ApplicationGroups.Expand([rule])));
    }
}
