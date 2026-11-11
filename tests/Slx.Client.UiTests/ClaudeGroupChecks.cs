using System.IO;
using Slx.Client.Core;
using Slx.Client.Desktop.ViewModels;
using Xunit;

namespace Slx.Client.UiTests;

internal static class ClaudeGroupChecks
{
    internal static void Run()
    {
        string desktop = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AnthropicClaude", "app-0.0.1", "claude.exe");
        string code = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Claude", "claude-code", "0.0.2", "claude.exe");
        var desktopRule = new AppRule(Guid.NewGuid(), "Claude Desktop", desktop, false);
        var codeRule = new AppRule(Guid.NewGuid(), "Claude Code", code, true);
        using var model = new MainViewModel(false, UiFixtures.Subscription, new ClientPreferences(Applications:
            [codeRule, desktopRule, codeRule with { Id = Guid.NewGuid() }]));
        var item = Assert.Single(model.Applications);
        Assert.Equal("Claude", item.Name);
        Assert.Equal(desktopRule.Id, item.Id);
        Assert.False(item.Enabled);
        Assert.Equal(new[] { desktop, code }, item.ToRules().Select(rule => rule.ExecutablePath));
        item.Enabled = true;
        Assert.All(item.ToRules(), rule => Assert.True(rule.Enabled));
        item.Enabled = false;
        Assert.All(item.ToRules(), rule => Assert.False(rule.Enabled));
        model.DiscoverySearch = "Claude";
        Assert.True(Assert.Single(model.FilteredLibrary, entry => entry.Name == "Claude").IsAdded);
    }
}
