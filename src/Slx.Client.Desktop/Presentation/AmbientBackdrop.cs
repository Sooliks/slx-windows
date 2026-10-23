using System.Windows;
using System.Windows.Media;
namespace Slx.Client.Desktop.Presentation;
/// <summary>
/// Site route scene (route-scene.ts) as a static native drawing: eight lines meet at one split and leave through the
/// tunnel (up) or directly (down). It illustrates the saved routing choice (mode and number of selected apps), never
/// traffic, speed or connection state. After a choice changes the branches ease once (650 ms); there is no loop.
/// </summary>
public sealed class AmbientBackdrop : FrameworkElement
{
    private const int Lines = 8, AppLines = 6;
    // Lower-right placement keeps page text on plain canvas; the site hero places the scene beside its copy instead.
    private const double SplitX = 0.9, SplitY = 0.78, VpnY = 0.3;
    public static readonly DependencyProperty AccentBrushProperty = BrushProperty(nameof(AccentBrush));
    public static readonly DependencyProperty EdgeBrushProperty = BrushProperty(nameof(EdgeBrush));
    public static readonly DependencyProperty CanvasBrushProperty = BrushProperty(nameof(CanvasBrush));
    public static readonly DependencyProperty RoutedAppsProperty = DependencyProperty.Register(nameof(RoutedApps), typeof(int),
        typeof(AmbientBackdrop), new PropertyMetadata(0, (d, _) => ((AmbientBackdrop)d).Retarget()));
    public static readonly DependencyProperty WholeRouteProperty = DependencyProperty.Register(nameof(WholeRoute), typeof(bool),
        typeof(AmbientBackdrop), new PropertyMetadata(false, (d, _) => ((AmbientBackdrop)d).Retarget()));
    public static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(nameof(Progress), typeof(double),
        typeof(AmbientBackdrop), new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));
    private readonly double[] from = new double[Lines], to = new double[Lines];
    public Brush? AccentBrush { get => (Brush?)GetValue(AccentBrushProperty); set => SetValue(AccentBrushProperty, value); }
    public Brush? EdgeBrush { get => (Brush?)GetValue(EdgeBrushProperty); set => SetValue(EdgeBrushProperty, value); }
    public Brush? CanvasBrush { get => (Brush?)GetValue(CanvasBrushProperty); set => SetValue(CanvasBrushProperty, value); }
    public int RoutedApps { get => (int)GetValue(RoutedAppsProperty); set => SetValue(RoutedAppsProperty, value); }
    public bool WholeRoute { get => (bool)GetValue(WholeRouteProperty); set => SetValue(WholeRouteProperty, value); }
    public double Progress { get => (double)GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }
    public AmbientBackdrop()
    {
        IsHitTestVisible = false;
        Focusable = false;
        SetResourceReference(AccentBrushProperty, "Accent");
        SetResourceReference(EdgeBrushProperty, "AmbientEdge");
        SetResourceReference(CanvasBrushProperty, "Background");
        Targets(to); Array.Copy(to, from, Lines);
    }
    /// <summary>Branch value per line, 0 = direct, 1 = through VPN. Two context lines always stay direct in app mode.</summary>
    internal static double[] Branches(int routedApps, bool whole)
    {
        var values = new double[Lines];
        for (var line = 0; line < Lines; line++) values[line] = whole || line < AppLines && line < Math.Max(0, routedApps) ? 1 : 0;
        return values;
    }
    private void Targets(double[] target) => Branches(RoutedApps, WholeRoute).CopyTo(target, 0);
    private void Retarget()
    {
        var progress = Progress;
        for (var line = 0; line < Lines; line++) from[line] += (to[line] - from[line]) * progress;
        Targets(to);
        MotionLifetime.Stop(this, this, ProgressProperty);
        Progress = 1;
        if (!Motion.IsEnabled(this) || from.SequenceEqual(to)) { Array.Copy(to, from, Lines); InvalidateVisual(); return; }
        MotionLifetime.Start(this, this, ProgressProperty, Motion.OneShot(0, 1, Motion.Route));
    }
    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo) { base.OnRenderSizeChanged(sizeInfo); InvalidateVisual(); }
    protected override void OnRender(DrawingContext drawing)
    {
        if (SystemParameters.HighContrast || AccentBrush is null || EdgeBrush is null || ActualWidth <= 0 || ActualHeight <= 0) return;
        double width = ActualWidth, height = ActualHeight, progress = Math.Clamp(Progress, 0, 1);
        drawing.PushClip(new RectangleGeometry(new Rect(RenderSize)));
        var split = new Point(SplitX * width, SplitY * height);
        var tunnel = new StreamGeometry();
        using (var context = tunnel.Open())
        {
            context.BeginFigure(split, false, false);
            context.BezierTo(new(Lerp(split.X, width * 1.04, 0.45), split.Y), new(Lerp(split.X, width * 1.04, 0.55), VpnY * height), new(width * 1.04, VpnY * height), true, true);
        }
        var routed = to.Zip(from, (target, start) => start + (target - start) * progress).ToArray();
        if (routed.Any(value => value > 0.5)) { drawing.PushOpacity(0.035); drawing.DrawGeometry(null, new Pen(AccentBrush, 26) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, tunnel); drawing.Pop(); }
        for (var line = 0; line < Lines; line++)
        {
            var vpn = routed[line];
            var geometry = Line(line, vpn, width, height);
            drawing.PushOpacity(Lerp(0.1, 0.3, vpn));
            drawing.DrawGeometry(null, new Pen(vpn > 0.5 ? AccentBrush : EdgeBrush, Lerp(1, 1.6, vpn)), geometry);
            drawing.Pop();
        }
        drawing.PushOpacity(0.5);
        var node = new Rect(split.X - 6, split.Y - 6, 12, 12);
        var nodePen = new Pen(AccentBrush, 1);
        drawing.DrawRectangle(null, nodePen, node);
        drawing.DrawLine(nodePen, node.BottomLeft, node.TopRight);
        drawing.Pop();
        // Site vignette: the reading side stays on plain canvas so no line crosses page text.
        if (CanvasBrush is SolidColorBrush canvas)
        {
            var fade = new LinearGradientBrush(canvas.Color, Color.FromArgb(0, canvas.Color.R, canvas.Color.G, canvas.Color.B), 0);
            fade.GradientStops[0].Offset = 0.42; fade.GradientStops[1].Offset = 0.86; fade.Freeze();
            drawing.DrawRectangle(fade, null, new Rect(RenderSize));
            var top = new LinearGradientBrush(canvas.Color, Color.FromArgb(0, canvas.Color.R, canvas.Color.G, canvas.Color.B), 90);
            top.GradientStops[0].Offset = 0.15; top.GradientStops[1].Offset = 0.5; top.Freeze();
            drawing.DrawRectangle(top, null, new Rect(RenderSize));
        }
        drawing.Pop();
    }
    private static Geometry Line(int index, double vpn, double width, double height)
    {
        var k = index / (double)(Lines - 1) - 0.5;
        var startX = 0.4 * width; var splitX = SplitX * width; var endX = 1.04 * width;
        var startY = (SplitY + k * 0.36) * height;
        var offset = k * 0.05 * height;
        var split = new Point(splitX, SplitY * height + offset);
        var endY = Lerp(1.0, VpnY, vpn) * height + offset * 1.6;
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(new(startX, startY), false, false);
            context.BezierTo(new(Lerp(startX, splitX, 0.55), startY), new(Lerp(startX, splitX, 0.7), split.Y), split, true, true);
            context.BezierTo(new(Lerp(splitX, endX, 0.45), split.Y), new(Lerp(splitX, endX, 0.55), endY), new(endX, endY), true, true);
        }
        geometry.Freeze();
        return geometry;
    }
    private static double Lerp(double a, double b, double t) => a + (b - a) * t;
    private static DependencyProperty BrushProperty(string name) => DependencyProperty.Register(name, typeof(Brush), typeof(AmbientBackdrop),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
}
