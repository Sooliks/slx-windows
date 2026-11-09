using Slx.Client.Core;
using Slx.Client.Windows;
namespace Slx.Client.Desktop.ViewModels;

public sealed partial class MainViewModel
{
    private void RefreshApplicationComposition()
    {
        var groups = Applications.Select(item => (Item: item, Rules: ExpandForDisplay(item))).ToArray();
        foreach (var group in groups)
        {
            var paths = group.Rules.Select(x => x.ExecutablePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
            group.Item.RefreshComposition(group.Rules, groups.Where(other => !ReferenceEquals(other.Item, group.Item)
                && other.Item.Enabled && other.Rules.Any(rule => paths.Contains(rule.ExecutablePath))).Select(other => other.Item.Name));
        }
    }
    private static IReadOnlyList<AppRule> ExpandForDisplay(AppItemViewModel item)
    {
        try { return ApplicationGroups.Expand(item.ToRules()); }
        catch (Exception error) when (error is ClientFault or System.IO.IOException or UnauthorizedAccessException)
        { return item.ToRules().ToArray(); } // Presentation cannot authorize or replace the real, validated connect plan.
    }
}
