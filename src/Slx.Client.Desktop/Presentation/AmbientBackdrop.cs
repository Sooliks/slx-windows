using System.Windows;
using System.Windows.Media;
namespace Slx.Client.Desktop.Presentation;
public sealed class AmbientBackdrop : FrameworkElement
{
    public static readonly DependencyProperty AccentBrushProperty = DependencyProperty.Register(
        nameof(AccentBrush), typeof(Brush), typeof(AmbientBackdrop), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public Brush? AccentBrush { get => (Brush?)GetValue(AccentBrushProperty); set => SetValue(AccentBrushProperty, value); }
    private static readonly Geometry Diagonals = CreateGeometry(
        "M620,125 L680,125 890,455 830,455 Z M920,125 L980,125 805,390 775,342 Z " +
        "M805,460 L835,412 1000,675 940,675 Z M620,675 L680,675 770,535 740,487 Z");
    private static readonly Geometry Contours = CreateGeometry(
        "M250,-50 L590,490 650,490 M225,-50 L570,505 650,505 M930,740 L720,405 M950,740 L750,422");
    public AmbientBackdrop()
    {
        IsHitTestVisible = false;
        Focusable = false;
        SetResourceReference(AccentBrushProperty, "Accent");
    }
    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo) { base.OnRenderSizeChanged(sizeInfo); InvalidateVisual(); }
    protected override void OnRender(DrawingContext drawing)
    {
        if (SystemParameters.HighContrast || AccentBrush is null || ActualWidth <= 0 || ActualHeight <= 0) return;
        drawing.PushClip(new RectangleGeometry(new Rect(RenderSize)));
        var scale = Math.Max(ActualWidth / 1000, ActualHeight / 800);
        drawing.PushTransform(new MatrixTransform(scale, 0, 0, scale, ActualWidth - 1000 * scale, ActualHeight - 800 * scale));
        drawing.PushOpacity(0.035); drawing.DrawGeometry(AccentBrush, null, Diagonals); drawing.Pop();
        drawing.PushOpacity(0.085); drawing.DrawGeometry(null, new Pen(AccentBrush, 1), Contours); drawing.Pop();
        drawing.Pop(); drawing.Pop();
    }
    private static Geometry CreateGeometry(string data) { var geometry = Geometry.Parse(data); geometry.Freeze(); return geometry; }
}
