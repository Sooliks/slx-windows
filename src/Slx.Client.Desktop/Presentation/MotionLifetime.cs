using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media.Animation;
namespace Slx.Client.Desktop.Presentation;
/// <summary>Owns only active one-shot clocks. Settled elements have no lifecycle or global-policy subscriptions.</summary>
internal sealed class MotionLifetime(UIElement context)
{
    private static readonly ConditionalWeakTable<UIElement, MotionLifetime> Owners = new();
    private readonly List<Running> running = [];
    private Window? window;
    private bool attached;
    internal static void Start(UIElement context, IAnimatable target, DependencyProperty property, DoubleAnimation animation)
    {
        var owner = Owners.GetValue(context, element => new(element));
        owner.Stop(target, property);
        if (!Motion.IsEnabled(context)) { target.BeginAnimation(property, null); return; }
        owner.Attach();
        var clock = animation.CreateClock();
        var item = new Running(target, property, clock);
        item.Completed = (_, _) => owner.Finish(item);
        owner.running.Add(item); clock.Completed += item.Completed;
        target.ApplyAnimationClock(property, clock, HandoffBehavior.SnapshotAndReplace);
    }
    internal static void Stop(UIElement context, IAnimatable target, DependencyProperty property)
    {
        if (Owners.TryGetValue(context, out var owner)) owner.Stop(target, property);
        target.BeginAnimation(property, null);
    }
    internal static bool IsTracking(UIElement context) => Owners.TryGetValue(context, out var owner) && owner.attached;
    private void Stop(IAnimatable target, DependencyProperty property)
    {
        foreach (var item in running.Where(item => ReferenceEquals(item.Target, target) && item.Property == property).ToArray()) Finish(item);
    }
    private void Finish(Running item)
    {
        if (!running.Remove(item)) return;
        item.Clock.Completed -= item.Completed;
        item.Target.ApplyAnimationClock(item.Property, null);
        item.Clock.Controller?.Remove();
        if (running.Count == 0) Detach();
    }
    private void StopAll()
    {
        foreach (var item in running.ToArray()) Finish(item);
    }
    private void Attach()
    {
        if (attached) return;
        attached = true;
        context.IsVisibleChanged += VisibilityChanged;
        if (context is FrameworkElement element) element.Unloaded += Unloaded;
        window = Window.GetWindow(context);
        if (window != null) { window.StateChanged += StateChanged; window.IsVisibleChanged += VisibilityChanged; }
        SystemParameters.StaticPropertyChanged += SystemChanged;
        Motion.PolicyChanged += PolicyChanged;
    }
    private void Detach()
    {
        if (!attached) return;
        attached = false;
        context.IsVisibleChanged -= VisibilityChanged;
        if (context is FrameworkElement element) element.Unloaded -= Unloaded;
        if (window != null) { window.StateChanged -= StateChanged; window.IsVisibleChanged -= VisibilityChanged; window = null; }
        SystemParameters.StaticPropertyChanged -= SystemChanged;
        Motion.PolicyChanged -= PolicyChanged;
    }
    private void VisibilityChanged(object sender, DependencyPropertyChangedEventArgs args) => CheckPolicy();
    private void StateChanged(object? sender, EventArgs args) => CheckPolicy();
    private void Unloaded(object sender, RoutedEventArgs args) => StopAll();
    private void SystemChanged(object? sender, PropertyChangedEventArgs args) => CheckPolicy();
    private void PolicyChanged(object? sender, EventArgs args) => CheckPolicy();
    private void CheckPolicy()
    {
        if (!context.Dispatcher.CheckAccess())
        {
            _ = context.Dispatcher.InvokeAsync(() => { if (attached && !Motion.IsEnabled(context)) StopAll(); });
            return;
        }
        if (attached && !Motion.IsEnabled(context)) StopAll();
    }
    private sealed class Running(IAnimatable target, DependencyProperty property, AnimationClock clock)
    {
        public IAnimatable Target { get; } = target;
        public DependencyProperty Property { get; } = property;
        public AnimationClock Clock { get; } = clock;
        public EventHandler? Completed { get; set; }
    }
}
