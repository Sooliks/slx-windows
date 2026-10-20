using System.Windows;
using System.Windows.Media;
namespace Slx.Client.Desktop.Presentation;
public sealed class AmbientBackdrop : FrameworkElement
{
    public static readonly DependencyProperty AccentBrushProperty = DependencyProperty.Register(
        nameof(AccentBrush), typeof(Brush), typeof(AmbientBackdrop), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty FaceBrushProperty = DependencyProperty.Register(
        nameof(FaceBrush), typeof(Brush), typeof(AmbientBackdrop), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty EdgeBrushProperty = DependencyProperty.Register(
        nameof(EdgeBrush), typeof(Brush), typeof(AmbientBackdrop), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public Brush? AccentBrush { get => (Brush?)GetValue(AccentBrushProperty); set => SetValue(AccentBrushProperty, value); }
    public Brush? FaceBrush { get => (Brush?)GetValue(FaceBrushProperty); set => SetValue(FaceBrushProperty, value); }
    public Brush? EdgeBrush { get => (Brush?)GetValue(EdgeBrushProperty); set => SetValue(EdgeBrushProperty, value); }
    private static readonly Geometry Diagonals = CreateGeometry(
        "M610,80 L678,80 912,438 844,438 Z M928,80 L996,80 814,358 780,306 Z " +
        "M814,442 L848,390 1034,720 966,720 Z M610,720 L678,720 780,554 746,502 Z");
    private static readonly Geometry Contours = CreateGeometry(
        "M270,-80 L558,368 614,368 M250,-80 L546,388 614,388 M1020,820 L816,500 M1040,820 L844,510");
    private static readonly Geometry Cuts = CreateGeometry("M790,377 L812,411 M763,452 L781,481");
    public AmbientBackdrop()
    {
        IsHitTestVisible = false;
        Focusable = false;
        SetResourceReference(AccentBrushProperty, "Accent");
        SetResourceReference(FaceBrushProperty, "AmbientFace");
        SetResourceReference(EdgeBrushProperty, "AmbientEdge");
    }
    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo) { base.OnRenderSizeChanged(sizeInfo); InvalidateVisual(); }
    protected override void OnRender(DrawingContext drawing)
    {
        if (SystemParameters.HighContrast || AccentBrush is null || ActualWidth <= 0 || ActualHeight <= 0) return;
        drawing.PushClip(new RectangleGeometry(new Rect(RenderSize)));
        var scale = Math.Max(ActualWidth / 1000, ActualHeight / 800);
        drawing.PushTransform(new MatrixTransform(scale, 0, 0, scale, ActualWidth - 1000 * scale, ActualHeight - 800 * scale));
        drawing.PushOpacity(0.075); drawing.DrawGeometry(FaceBrush, null, Diagonals); drawing.Pop();
        drawing.PushOpacity(0.09); drawing.DrawGeometry(null, new Pen(EdgeBrush, 1), Contours); drawing.Pop();
        drawing.PushOpacity(0.22); drawing.DrawGeometry(null, new Pen(AccentBrush, 1), Cuts); drawing.Pop();
        drawing.Pop(); drawing.Pop();
    }
    private static Geometry CreateGeometry(string data) { var geometry = Geometry.Parse(data); geometry.Freeze(); return geometry; }
}
