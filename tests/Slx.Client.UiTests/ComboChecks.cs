using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using Slx.Client.Desktop;
using Slx.Client.Desktop.ViewModels;
using Xunit;
namespace Slx.Client.UiTests;
internal static class ComboChecks
{
    public static void Run(MainWindow window, MainViewModel model)
    {
        var combo = Sta.Descendants<ComboBox>(window).Single(x => x.Name == "ConnectionProfile");
        Assert.Equal(UiFixtures.Primary, combo.SelectedItem);
        AssertDisplay(combo, UiFixtures.Primary.Name);
        combo.SelectedIndex = 1; Sta.Pump(); AssertDisplay(combo, UiFixtures.Alternative.Name);
        combo.IsDropDownOpen = true; Sta.Pump();
        var popup = (Popup)combo.Template.FindName("PART_Popup", combo);
        Assert.Equal(2, combo.Items.Count);
        var popupText = string.Join(" ", Sta.Descendants<TextBlock>(popup.Child).Select(x => x.Text));
        Assert.Contains(UiFixtures.Primary.Name, popupText); Assert.Contains(UiFixtures.Alternative.Name, popupText);
        Assert.DoesNotContain("VpnProfile", popupText);
        UiScreenshots.Save((FrameworkElement)popup.Child, "slx-populated-profile-menu");
        combo.IsDropDownOpen = false; Sta.Pump();
        combo.SelectedIndex = 0; Sta.Pump();
        model.Page = "settings"; Sta.Pump();
        var settings = Sta.Descendants<ComboBox>(window).Single(x => x.Name == "ProfileSelect");
        AssertDisplay(settings, UiFixtures.Primary.Name);
        var language = Sta.Descendants<ComboBox>(window).Single(x => x.Name == "LanguageSelect");
        AssertDisplay(language, "Русский"); language.SelectedValue = "en"; Sta.Pump(); AssertDisplay(language, "English");
        model.Page = "connection"; model.Language = "ru"; Sta.Pump();
        GenericTemplates();
    }
    private static void GenericTemplates()
    {
        var combo = new ComboBox { ItemsSource = new[] { UiFixtures.Primary, UiFixtures.Alternative }, DisplayMemberPath = "Name", SelectedIndex = 0, Margin = new Thickness(20), VerticalAlignment = VerticalAlignment.Center };
        var host = new Window { Content = combo, Width = 450, Height = 130, ShowInTaskbar = false, Left = -20000, Top = -20000 };
        host.Show(); Sta.Pump();
        try
        {
            AssertDisplay(combo, UiFixtures.Primary.Name);
            combo.DisplayMemberPath = "";
            combo.ItemTemplate = (DataTemplate)XamlReader.Parse("<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><TextBlock Text='{Binding Name}' TextTrimming='CharacterEllipsis'/></DataTemplate>");
            combo.SelectedIndex = 1; Sta.Pump(); AssertDisplay(combo, UiFixtures.Alternative.Name);
        }
        finally { host.Close(); Sta.Pump(); }
    }
    private static void AssertDisplay(ComboBox combo, string expected)
    {
        var text = string.Join(" ", Sta.Descendants<TextBlock>(combo).Select(x => x.Text));
        Assert.Contains(expected, text); Assert.DoesNotContain("VpnProfile", text);
        Assert.DoesNotContain("not-a-real-profile-secret", text);
    }
}
