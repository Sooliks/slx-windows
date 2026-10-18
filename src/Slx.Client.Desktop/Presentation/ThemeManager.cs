using System.Windows;
using System.Windows.Media;
namespace Slx.Client.Desktop.Presentation;
public static class ThemeManager
{
    public static void Apply(string name)
    {
        var resources = Application.Current.Resources.MergedDictionaries;
        resources[0] = new ResourceDictionary { Source = new Uri($"Themes/{(name == "terminal" ? "Terminal" : "Olive")}.xaml", UriKind.Relative) };
        if (!SystemParameters.HighContrast) return;
        foreach (var key in new[] { "Background", "Surface", "SurfaceRaised" })
            Application.Current.Resources[key] = SystemColors.WindowBrush;
        Application.Current.Resources["Text"] = SystemColors.WindowTextBrush;
        Application.Current.Resources["Muted"] = SystemColors.WindowTextBrush;
        Application.Current.Resources["Border"] = SystemColors.WindowTextBrush;
        Application.Current.Resources["Accent"] = SystemColors.HighlightBrush;
        Application.Current.Resources["AccentText"] = SystemColors.HighlightTextBrush;
    }
}
