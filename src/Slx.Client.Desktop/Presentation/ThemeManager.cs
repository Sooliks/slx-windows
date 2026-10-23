using System.ComponentModel;
using System.Windows;
namespace Slx.Client.Desktop.Presentation;
public static class ThemeManager
{
    private static Application? application;
    private static ResourceDictionary? contrastLayer;
    internal static Func<bool>? HighContrastOverride { get; set; }
    // High contrast maps every semantic token to Windows system colors; translucent glass and washes become opaque.
    private static readonly string[] WindowKeys = ["Background", "Surface", "SurfaceRaised", "SurfaceHover", "SurfaceInset", "ControlFill",
        "ControlInset", "AccentWash", "AccentWashStrong", "SuccessWash", "DangerWash", "SignalSurface", "GlassSurface", "Backdrop", "ControlHighlight"];
    private static readonly string[] TextKeys = ["Text", "Muted", "Success", "Danger", "Border", "ControlBorder", "ButtonBorder",
        "ControlHoverBorder", "GlassEdge", "AccentLine", "DangerLine", "ScrollThumb", "ScrollThumbHover", "AmbientFace", "AmbientEdge"];
    public static void Apply(string name)
    {
        Observe(Application.Current);
        var resources = Application.Current.Resources.MergedDictionaries;
        resources[0] = new ResourceDictionary { Source = new Uri($"/{typeof(ThemeManager).Assembly.GetName().Name};component/Themes/{(name == "terminal" ? "Terminal" : "Olive")}.xaml", UriKind.Relative) };
        RefreshContrast();
    }
    // Keep accessibility colors in an owned layer: removing it restores the selected theme without touching any
    // application resources supplied by another owner. DynamicResource consumers update in the existing window.
    internal static void RefreshContrast()
    {
        var app = application;
        if (app is null || app.Dispatcher.HasShutdownStarted) return;
        if (!app.Dispatcher.CheckAccess()) { _ = app.Dispatcher.InvokeAsync(RefreshContrast); return; }
        if (contrastLayer is not null) app.Resources.MergedDictionaries.Remove(contrastLayer);
        contrastLayer = null;
        if (!(HighContrastOverride?.Invoke() ?? SystemParameters.HighContrast)) return;
        var layer = new ResourceDictionary();
        foreach (var key in WindowKeys) layer[key] = SystemColors.WindowBrush;
        foreach (var key in TextKeys) layer[key] = SystemColors.WindowTextBrush;
        layer["Accent"] = SystemColors.HighlightBrush;
        layer["AccentHover"] = SystemColors.HighlightBrush;
        layer["AccentText"] = SystemColors.HighlightTextBrush;
        contrastLayer = layer;
        app.Resources.MergedDictionaries.Add(layer);
    }
    private static void Observe(Application app)
    {
        if (ReferenceEquals(application, app)) return;
        StopObserving(); application = app;
        SystemParameters.StaticPropertyChanged += PolicyChanged;
        app.Exit += ApplicationExited;
    }
    private static void PolicyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (string.IsNullOrEmpty(args.PropertyName) || args.PropertyName == nameof(SystemParameters.HighContrast)) RefreshContrast();
    }
    private static void ApplicationExited(object sender, ExitEventArgs args) => StopObserving();
    private static void StopObserving()
    {
        SystemParameters.StaticPropertyChanged -= PolicyChanged;
        if (application is { } app)
        {
            app.Exit -= ApplicationExited;
            if (contrastLayer is not null) app.Resources.MergedDictionaries.Remove(contrastLayer);
        }
        contrastLayer = null; application = null;
    }
}
