using System.Windows;
using System.Windows.Media;
using Slx.Client.Desktop.Presentation;
using Xunit;
namespace Slx.Client.UiTests;
internal static class ThemeContrastChecks
{
    public static void Run()
    {
        var app = Application.Current;
        var previousTheme = app.Resources.MergedDictionaries[0].Source.OriginalString.Contains("Terminal", StringComparison.Ordinal) ? "terminal" : "olive";
        var previousOverride = ThemeManager.HighContrastOverride;
        var sentinel = new object();
        var foreignLayer = new ResourceDictionary { ["ContrastFixtureMerged"] = sentinel };
        app.Resources["ContrastFixtureRoot"] = sentinel;
        app.Resources.MergedDictionaries.Add(foreignLayer);
        try
        {
            ThemeManager.HighContrastOverride = () => false;
            ThemeManager.Apply("olive");
            var layerCount = app.Resources.MergedDictionaries.Count;
            Assert.Equal(Color.FromRgb(0x0F, 0x11, 0x0E), Brush("Background").Color);
            ThemeManager.HighContrastOverride = () => true;
            ThemeManager.RefreshContrast();
            Assert.Equal(layerCount + 1, app.Resources.MergedDictionaries.Count);
            Assert.Same(SystemColors.WindowBrush, app.FindResource("GlassSurface"));
            Assert.Same(SystemColors.WindowTextBrush, app.FindResource("Text"));
            Assert.Same(SystemColors.HighlightTextBrush, app.FindResource("AccentText"));
            // Changing the selected theme during high contrast must keep the accessibility layer singular.
            ThemeManager.Apply("terminal");
            Assert.Equal(layerCount + 1, app.Resources.MergedDictionaries.Count);
            Assert.Same(SystemColors.WindowBrush, app.FindResource("Background"));
            ThemeManager.HighContrastOverride = () => false;
            ThemeManager.RefreshContrast();
            Assert.Equal(layerCount, app.Resources.MergedDictionaries.Count);
            Assert.Equal(Colors.Black, Brush("Background").Color);
            Assert.Equal(Color.FromRgb(0xF2, 0xF2, 0xF2), Brush("Accent").Color);
            Assert.Same(sentinel, app.FindResource("ContrastFixtureRoot"));
            Assert.Same(sentinel, app.FindResource("ContrastFixtureMerged"));
            // Repeated refresh must neither accumulate dictionaries nor leave stale overrides behind.
            ThemeManager.RefreshContrast();
            ThemeManager.Apply("olive");
            Assert.Equal(layerCount, app.Resources.MergedDictionaries.Count);
            Assert.Equal(Color.FromRgb(0xE6, 0xF3, 0x5A), Brush("Accent").Color);
        }
        finally
        {
            ThemeManager.HighContrastOverride = previousOverride;
            ThemeManager.Apply(previousTheme);
            app.Resources.Remove("ContrastFixtureRoot");
            app.Resources.MergedDictionaries.Remove(foreignLayer);
        }
    }
    private static SolidColorBrush Brush(string key) => Assert.IsType<SolidColorBrush>(Application.Current.FindResource(key));
}
