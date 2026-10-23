using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
namespace Slx.Client.Desktop.Presentation;
/// <summary>
/// Pages are recreated on navigation; this keeps each page's reading position for the lifetime of its view model.
/// Offsets live only in memory and are dropped together with the model.
/// </summary>
public static class ScrollMemory
{
    private static readonly ConditionalWeakTable<object, Dictionary<string, double>> Offsets = new();
    public static readonly DependencyProperty KeyProperty = DependencyProperty.RegisterAttached("Key", typeof(string), typeof(ScrollMemory),
        new PropertyMetadata(null, OnKeyChanged));
    // Unloaded runs after the inherited DataContext is cleared, so the owner is captured while the page is attached and
    // every user scroll is recorded immediately. Saving therefore never depends on Unloaded ordering.
    private static readonly DependencyProperty OwnerProperty = DependencyProperty.RegisterAttached("Owner", typeof(object), typeof(ScrollMemory),
        new PropertyMetadata(null));
    private static readonly DependencyProperty TrackingProperty = DependencyProperty.RegisterAttached("Tracking", typeof(bool), typeof(ScrollMemory),
        new PropertyMetadata(false));
    public static string? GetKey(DependencyObject element) => (string?)element.GetValue(KeyProperty);
    public static void SetKey(DependencyObject element, string? value) => element.SetValue(KeyProperty, value);
    private static void OnKeyChanged(DependencyObject element, DependencyPropertyChangedEventArgs change)
    {
        if (element is not ScrollViewer scroll) return;
        scroll.Loaded -= Restore; scroll.Unloaded -= Detach; scroll.DataContextChanged -= Adopt; scroll.ScrollChanged -= Record;
        if (change.NewValue is not string) return;
        scroll.Loaded += Restore; scroll.Unloaded += Detach; scroll.DataContextChanged += Adopt; scroll.ScrollChanged += Record;
    }
    internal static double? Saved(object owner, string key) =>
        Offsets.TryGetValue(owner, out var pages) && pages.TryGetValue(key, out var offset) ? offset : null;
    private static void Adopt(object sender, DependencyPropertyChangedEventArgs change)
    {
        if (sender is ScrollViewer scroll && change.NewValue is { } owner) scroll.SetValue(OwnerProperty, owner);
    }
    private static void Record(object sender, ScrollChangedEventArgs args)
    {
        // Only this page's own viewport, and only once its saved position has been restored.
        if (sender is not ScrollViewer scroll || !ReferenceEquals(args.OriginalSource, scroll) || scroll.GetValue(TrackingProperty) is not true) return;
        Store(scroll);
    }
    private static void Detach(object sender, RoutedEventArgs args)
    {
        if (sender is not ScrollViewer scroll) return;
        if (scroll.GetValue(TrackingProperty) is true) Store(scroll);
        scroll.SetValue(TrackingProperty, false);
    }
    private static void Store(ScrollViewer scroll)
    {
        if (scroll.GetValue(OwnerProperty) is { } owner && GetKey(scroll) is { } key) Offsets.GetOrCreateValue(owner)[key] = scroll.VerticalOffset;
    }
    private static void Restore(object sender, RoutedEventArgs args)
    {
        if (sender is not ScrollViewer { DataContext: { } owner } scroll || GetKey(scroll) is not { } key) return;
        scroll.SetValue(OwnerProperty, owner); scroll.SetValue(TrackingProperty, false);
        var offset = Saved(owner, key) ?? 0;
        // Layout has measured the page by Loaded; a shorter page clamps to its own extent. Tracking starts afterwards,
        // so the initial zero offset of a freshly created page cannot overwrite the saved position.
        _ = scroll.Dispatcher.InvokeAsync(() =>
        {
            if (!scroll.IsLoaded) return;
            if (offset > 0) { scroll.ScrollToVerticalOffset(offset); scroll.UpdateLayout(); }
            scroll.SetValue(TrackingProperty, true);
        }, DispatcherPriority.Loaded);
    }
}
