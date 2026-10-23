using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;
namespace Slx.Client.Desktop.Presentation;
/// <summary>
/// Transient activity copy ("Please wait…", "Applying changes…") without layout jumps or flashes. The element keeps
/// its space (Hidden, never Collapsed); it appears only when the activity lasts longer than <see cref="Delay"/> and,
/// once shown, stays at least <see cref="Minimum"/> so a half-second operation never blinks text in and out.
/// It only presents activity that the view model reports; it never extends or invents a state.
/// </summary>
public static class Presence
{
    public static readonly TimeSpan Delay = TimeSpan.FromMilliseconds(400);
    public static readonly TimeSpan Minimum = TimeSpan.FromMilliseconds(700);
    public static readonly DependencyProperty ShowProperty = DependencyProperty.RegisterAttached("Show", typeof(bool), typeof(Presence),
        new PropertyMetadata(false, OnShowChanged));
    private static readonly DependencyProperty StateProperty = DependencyProperty.RegisterAttached("State", typeof(PresenceState),
        typeof(Presence), new PropertyMetadata(null));
    public static bool GetShow(DependencyObject element) => (bool)element.GetValue(ShowProperty);
    public static void SetShow(DependencyObject element, bool value) => element.SetValue(ShowProperty, value);
    internal static bool IsShown(DependencyObject element) => element.GetValue(StateProperty) is PresenceState { Visible: true };
    private sealed class PresenceState
    {
        public DispatcherTimer? Timer;
        public long ShownAt;
        public bool Visible;
    }
    private static void OnShowChanged(DependencyObject element, DependencyPropertyChangedEventArgs change)
    {
        if (element is not FrameworkElement target) return;
        if (target.GetValue(StateProperty) is not PresenceState state)
        {
            state = new PresenceState();
            target.SetValue(StateProperty, state);
            target.Unloaded += (_, _) => Stop(state);
            // A reattached element shows its current activity at once instead of waiting for the next change.
            target.Loaded += (_, _) => { Stop(state); Apply(target, state, GetShow(target)); };
        }
        Stop(state);
        var wanted = change.NewValue is true;
        if (wanted == state.Visible) return;
        // Render checks and fixtures observe final states synchronously.
        if (Motion.Suppressed) { Apply(target, state, wanted); return; }
        var wait = wanted ? Delay : Minimum - System.Diagnostics.Stopwatch.GetElapsedTime(state.ShownAt);
        if (wait <= TimeSpan.Zero) { Apply(target, state, wanted); return; }
        var timer = new DispatcherTimer(DispatcherPriority.Normal, target.Dispatcher) { Interval = wait };
        timer.Tick += (_, _) => { Stop(state); if (GetShow(target) == wanted) Apply(target, state, wanted); };
        state.Timer = timer;
        timer.Start();
    }
    private static void Apply(FrameworkElement target, PresenceState state, bool visible)
    {
        state.Visible = visible;
        if (visible) state.ShownAt = System.Diagnostics.Stopwatch.GetTimestamp();
        target.Visibility = visible ? Visibility.Visible : Visibility.Hidden;
        if (visible) Motion.Enter(target, 0);
    }
    private static void Stop(PresenceState state)
    {
        state.Timer?.Stop();
        state.Timer = null;
    }
}
/// <summary>Reserves an element's space while it is hidden, so appearing content never shifts the page.</summary>
public sealed class VisibleOrHiddenConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Visibility.Visible : Visibility.Hidden;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
