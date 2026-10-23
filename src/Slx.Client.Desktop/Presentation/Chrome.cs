using System.Windows;
using System.Windows.Media;
namespace Slx.Client.Desktop.Presentation;
/// <summary>Per-style interaction fills read by the shared control templates (site hover tokens).</summary>
public static class Chrome
{
    public static readonly DependencyProperty HoverProperty = DependencyProperty.RegisterAttached("Hover", typeof(Brush), typeof(Chrome),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty HoverEdgeProperty = DependencyProperty.RegisterAttached("HoverEdge", typeof(Brush), typeof(Chrome),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public static Brush? GetHover(DependencyObject element) => (Brush?)element.GetValue(HoverProperty);
    public static void SetHover(DependencyObject element, Brush? value) => element.SetValue(HoverProperty, value);
    public static Brush? GetHoverEdge(DependencyObject element) => (Brush?)element.GetValue(HoverEdgeProperty);
    public static void SetHoverEdge(DependencyObject element, Brush? value) => element.SetValue(HoverEdgeProperty, value);
}
