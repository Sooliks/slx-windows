using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
namespace Slx.Client.Desktop.Presentation;
/// <summary>
/// One owner for short interface motion (site tokens: 140 ms controls, 180 ms entry, 220 ms choices,
/// 360 ms confirmation, 650 ms route). Every animation is a one-shot from the current value; nothing loops.
/// When motion is unavailable (Windows animations off, High Contrast, hidden or minimized window, render
/// checks) the final value is applied synchronously, so state never depends on an animation finishing.
/// </summary>
public static class Motion
{
    public static readonly Duration Fast = new(TimeSpan.FromMilliseconds(140));
    public static readonly Duration Entry = new(TimeSpan.FromMilliseconds(180));
    public static readonly Duration Choice = new(TimeSpan.FromMilliseconds(220));
    public static readonly Duration Confirm = new(TimeSpan.FromMilliseconds(360));
    public static readonly Duration Route = new(TimeSpan.FromMilliseconds(650));
    public static readonly IEasingFunction Ease = new SiteEase();
    /// <summary>Render checks and UI fixtures capture exact final frames.</summary>
    private static bool suppressed;
    private static Func<bool>? systemPreferenceOverride;
    internal static event EventHandler? PolicyChanged;
    public static bool Suppressed { get => suppressed; set { suppressed = value; PolicyChanged?.Invoke(null, EventArgs.Empty); } }
    internal static Func<bool>? SystemPreferenceOverride
    {
        get => systemPreferenceOverride;
        set { systemPreferenceOverride = value; PolicyChanged?.Invoke(null, EventArgs.Empty); }
    }

    public static readonly DependencyProperty FadeToProperty = DependencyProperty.RegisterAttached("FadeTo", typeof(double),
        typeof(Motion), new PropertyMetadata(double.NaN, (d, e) => Animate(d as UIElement, UIElement.OpacityProperty, e, Fast)));
    public static readonly DependencyProperty ShiftXProperty = DependencyProperty.RegisterAttached("ShiftX", typeof(double),
        typeof(Motion), new PropertyMetadata(0.0, (d, e) => AnimateShift(d as UIElement, TranslateTransform.XProperty, e)));
    public static readonly DependencyProperty ShiftYProperty = DependencyProperty.RegisterAttached("ShiftY", typeof(double),
        typeof(Motion), new PropertyMetadata(0.0, (d, e) => AnimateShift(d as UIElement, TranslateTransform.YProperty, e)));
    public static readonly DependencyProperty RevealProperty = DependencyProperty.RegisterAttached("Reveal", typeof(bool),
        typeof(Motion), new PropertyMetadata(false, OnRevealChanged));
    public static readonly DependencyProperty ConfirmWhenProperty = DependencyProperty.RegisterAttached("ConfirmWhen", typeof(bool),
        typeof(Motion), new PropertyMetadata(false, OnConfirmChanged));
    public static double GetFadeTo(DependencyObject element) => (double)element.GetValue(FadeToProperty);
    public static void SetFadeTo(DependencyObject element, double value) => element.SetValue(FadeToProperty, value);
    public static double GetShiftX(DependencyObject element) => (double)element.GetValue(ShiftXProperty);
    public static void SetShiftX(DependencyObject element, double value) => element.SetValue(ShiftXProperty, value);
    public static double GetShiftY(DependencyObject element) => (double)element.GetValue(ShiftYProperty);
    public static void SetShiftY(DependencyObject element, double value) => element.SetValue(ShiftYProperty, value);
    public static bool GetReveal(DependencyObject element) => (bool)element.GetValue(RevealProperty);
    public static void SetReveal(DependencyObject element, bool value) => element.SetValue(RevealProperty, value);
    public static bool GetConfirmWhen(DependencyObject element) => (bool)element.GetValue(ConfirmWhenProperty);
    public static void SetConfirmWhen(DependencyObject element, bool value) => element.SetValue(ConfirmWhenProperty, value);

    public static bool IsEnabled(DependencyObject? context)
    {
        if (Suppressed || SystemParameters.HighContrast) return false;
        if (!(SystemPreferenceOverride?.Invoke() ?? SystemParameters.ClientAreaAnimation)) return false;
        if (context is not UIElement element || !element.IsVisible) return false;
        var window = Window.GetWindow(element);
        return window is null || window.IsVisible && window.WindowState != WindowState.Minimized;
    }
    /// <summary>Fades and lifts an element that just became visible. Ends on its base values (Opacity 1, offset 0).</summary>
    public static void Enter(UIElement element, double rise = 6)
    {
        var shift = Translate(element);
        if (!IsEnabled(element))
        {
            MotionLifetime.Stop(element, element, UIElement.OpacityProperty);
            if (shift != null) MotionLifetime.Stop(element, shift, TranslateTransform.YProperty);
            return;
        }
        var opacity = (double)element.GetAnimationBaseValue(UIElement.OpacityProperty);
        MotionLifetime.Start(element, element, UIElement.OpacityProperty, OneShot(0, opacity, Entry));
        if (shift is null) return;
        var rest = (double)shift.GetAnimationBaseValue(TranslateTransform.YProperty);
        MotionLifetime.Start(element, shift, TranslateTransform.YProperty, OneShot(rest + rise, rest, Entry));
    }
    internal static DoubleAnimation OneShot(double from, double to, Duration duration) =>
        new(from, to, duration) { EasingFunction = Ease, FillBehavior = FillBehavior.Stop };
    private static void Animate(UIElement? element, DependencyProperty property, DependencyPropertyChangedEventArgs change, Duration duration)
    {
        if (element is null || change.NewValue is not double target || double.IsNaN(target)) return;
        Run(element, element, property, target, duration, change.OldValue is double old && !double.IsNaN(old));
    }
    private static void AnimateShift(UIElement? element, DependencyProperty property, DependencyPropertyChangedEventArgs change)
    {
        if (element is null || change.NewValue is not double target) return;
        var shift = Translate(element);
        if (shift is not null) Run(shift, element, property, target, Choice, true);
    }
    // The base value is written first and the animation runs from the current (possibly animated) value with
    // FillBehavior.Stop, so an interrupted or finished animation never holds a stale value or a live clock.
    private static void Run(IAnimatable target, UIElement context, DependencyProperty property, double value, Duration duration, bool animate)
    {
        var owner = (DependencyObject)target;
        var current = (double)owner.GetValue(property);
        owner.SetValue(property, value);
        if (!animate || !IsEnabled(context) || Math.Abs(current - value) < 0.001) { MotionLifetime.Stop(context, target, property); return; }
        MotionLifetime.Start(context, target, property, OneShot(current, value, duration));
    }
    internal static TranslateTransform? Translate(UIElement element)
    {
        if (element.RenderTransform is TranslateTransform existing && !existing.IsFrozen) return existing;
        if (element.RenderTransform is not null && element.RenderTransform != Transform.Identity && element.RenderTransform is not TranslateTransform) return null;
        var created = new TranslateTransform();
        element.RenderTransform = created;
        return created;
    }
    private static void OnRevealChanged(DependencyObject element, DependencyPropertyChangedEventArgs change)
    {
        if (element is not UIElement target) return;
        target.IsVisibleChanged -= Revealed;
        if (change.NewValue is true) target.IsVisibleChanged += Revealed;
    }
    private static void Revealed(object sender, DependencyPropertyChangedEventArgs change)
    {
        if (sender is UIElement element && change.NewValue is true) Enter(element);
    }
    private static void OnConfirmChanged(DependencyObject element, DependencyPropertyChangedEventArgs change)
    {
        // A confirmed state change draws one ring. The ring is decoration; the state itself is already applied.
        if (element is not UIElement ring) return;
        if (change.NewValue is not true)
        {
            MotionLifetime.Stop(ring, ring, UIElement.OpacityProperty);
            if (ring.RenderTransform is ScaleTransform previous)
            {
                MotionLifetime.Stop(ring, previous, ScaleTransform.ScaleXProperty);
                MotionLifetime.Stop(ring, previous, ScaleTransform.ScaleYProperty);
            }
            return;
        }
        if (change.OldValue is not false || !IsEnabled(ring)) return;
        var scale = ring.RenderTransform as ScaleTransform;
        if (scale is null || scale.IsFrozen) { scale = new ScaleTransform(); ring.RenderTransformOrigin = new Point(0.5, 0.5); ring.RenderTransform = scale; }
        MotionLifetime.Start(ring, ring, UIElement.OpacityProperty, OneShot(0.6, 0, Confirm));
        MotionLifetime.Start(ring, scale, ScaleTransform.ScaleXProperty, OneShot(1, 2.6, Confirm));
        MotionLifetime.Start(ring, scale, ScaleTransform.ScaleYProperty, OneShot(1, 2.6, Confirm));
    }
    /// <summary>CSS cubic-bezier(0.2, 0.7, 0.2, 1), the site's --motion-ease.</summary>
    private sealed class SiteEase : IEasingFunction
    {
        public double Ease(double time)
        {
            if (time <= 0) return 0;
            if (time >= 1) return 1;
            double t = time;
            for (var step = 0; step < 8; step++)
            {
                var x = Bezier(t, 0.2, 0.2) - time;
                var slope = Slope(t, 0.2, 0.2);
                if (Math.Abs(x) < 1e-5 || Math.Abs(slope) < 1e-6) break;
                t = Math.Clamp(t - x / slope, 0, 1);
            }
            return Bezier(t, 0.7, 1);
        }
        private static double Bezier(double t, double p1, double p2) => 3 * (1 - t) * (1 - t) * t * p1 + 3 * (1 - t) * t * t * p2 + t * t * t;
        private static double Slope(double t, double p1, double p2) => 3 * (1 - t) * (1 - t) * p1 + 6 * (1 - t) * t * (p2 - p1) + 3 * t * t * (1 - p2);
    }
}
