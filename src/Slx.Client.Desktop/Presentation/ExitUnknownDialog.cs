using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Slx.Client.Desktop.Localization;
namespace Slx.Client.Desktop.Presentation;
internal sealed class ExitUnknownDialog : Window
{
    public ExitUnknownDialog(Window owner)
    {
        Owner = owner; SetResourceReference(FontFamilyProperty, "FontText"); FontSize = 14; FontWeight = FontWeights.Medium; Title = "SLX VPN"; Width = 490; SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; ShowInTaskbar = false;
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        TextOptions.SetTextRenderingMode(this, TextRenderingMode.Grayscale); UseLayoutRounding = true;
        SetResourceReference(BackgroundProperty, "Background"); SetResourceReference(ForegroundProperty, "Text");
        var panel = new StackPanel { Margin = new Thickness(26) };
        var title = new TextBlock { Text = Text.Catalog["ExitUnknownTitle"], FontSize = 21, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 14) };
        title.SetResourceReference(TextBlock.FontFamilyProperty, "FontDisplay");
        panel.Children.Add(title);
        panel.Children.Add(new TextBlock { Text = Text.Catalog["ExitUnknownBody"], TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 24), LineHeight = 23 });
        var actions = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Content = Text.Catalog["Cancel"], IsDefault = true, IsCancel = true, Margin = new Thickness(0, 0, 10, 0) };
        var close = new Button { Content = Text.Catalog["ExitUiOnly"], Style = (Style)FindResource("DangerButton") };
        close.Click += (_, _) => DialogResult = true;
        actions.Children.Add(cancel); actions.Children.Add(close); panel.Children.Add(actions);
        Content = new Border { Child = panel, BorderThickness = new Thickness(1) };
        ((Border)Content).SetResourceReference(Border.BorderBrushProperty, "ControlBorder");
        Loaded += (_, _) => cancel.Focus();
    }
}
