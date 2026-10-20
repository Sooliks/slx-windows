using Slx.Client.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Media;
namespace Slx.Client.Desktop.Presentation;
public sealed class LibraryEntry(string Name, AppRule? Rule, CatalogApplication? Catalog, IReadOnlyList<AppRule>? Paths = null) : ObservableObject, IDisposable
{
    private ImageSource? icon;
    private LocalIconRequest? iconRequest;
    private bool disposed;
    public string Name { get; } = Name;
    public AppRule? Rule { get; private set; } = Rule;
    public CatalogApplication? Catalog { get; } = Catalog;
    public IReadOnlyList<AppRule>? Paths { get; private set; } = Paths;
    internal Func<string, CancellationToken, Task<ImageSource>> IconLoader { get; init; } = LocalIcon.LoadAsync;
    public string Detail=>Paths is {Count:>0}?string.Join(Environment.NewLine,Paths.Select(x=>x.ExecutablePath)):Rule?.ExecutablePath??string.Join(", ",Catalog?.Executables??[]);
    public ImageSource Icon
    {
        get
        {
            if (icon is not null) return icon;
            var paths = IconPaths();
            icon = LocalIcon.Cached(paths);
            if (!disposed && Rule is not null)
                iconRequest = LocalIconRequest.Start(this, paths,
                    static (entry, image) => entry.SetProperty(ref entry.icon, image, nameof(Icon)), IconLoader);
            return icon;
        }
    }
    public string Action=>Localization.Text.Catalog[Rule is null?"FindExecutable":"Add"];
    public void RefreshLanguage() => OnPropertyChanged(nameof(Action));
    public void UpdatePaths(AppRule primary, IReadOnlyList<AppRule> paths)
    {
        if (disposed) return;
        var previousPaths = IconPaths();
        Rule = primary; Paths = paths;
        OnPropertyChanged(nameof(Rule)); OnPropertyChanged(nameof(Paths)); OnPropertyChanged(nameof(Detail));
        if (ReferenceEquals(icon, LocalIcon.Fallback) || !previousPaths.SequenceEqual(IconPaths(), StringComparer.OrdinalIgnoreCase))
            ResetIcon();
    }
    internal void RefreshIconIfMissing()
    {
        if (!disposed && ReferenceEquals(icon, LocalIcon.Fallback)) ResetIcon();
    }
    private void ResetIcon()
    {
        iconRequest?.Dispose(); iconRequest = null; icon = null;
        OnPropertyChanged(nameof(Icon));
    }
    private IReadOnlyList<string> IconPaths() => (Rule is null ? [] : new[] { Rule.ExecutablePath })
        .Concat(Paths?.Select(path => path.ExecutablePath) ?? []).Distinct(StringComparer.OrdinalIgnoreCase).Take(64).ToArray();
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; iconRequest?.Dispose(); iconRequest = null;
    }
}
