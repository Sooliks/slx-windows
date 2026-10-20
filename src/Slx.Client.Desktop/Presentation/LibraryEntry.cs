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
            icon = LocalIcon.Cached(Rule?.ExecutablePath ?? "");
            if (!disposed && Rule is not null)
                iconRequest = LocalIconRequest.Start(this, Rule.ExecutablePath,
                    static (entry, image) => entry.SetProperty(ref entry.icon, image, nameof(Icon)), IconLoader);
            return icon;
        }
    }
    public string Action=>Localization.Text.Catalog[Rule is null?"FindExecutable":"Add"];
    public void RefreshLanguage() => OnPropertyChanged(nameof(Action));
    public void UpdatePaths(AppRule primary, IReadOnlyList<AppRule> paths)
    {
        if (disposed) return;
        bool iconChanged = !string.Equals(Rule?.ExecutablePath, primary.ExecutablePath, StringComparison.OrdinalIgnoreCase);
        Rule = primary; Paths = paths;
        OnPropertyChanged(nameof(Rule)); OnPropertyChanged(nameof(Paths)); OnPropertyChanged(nameof(Detail));
        if (iconChanged)
        {
            iconRequest?.Dispose(); iconRequest = null; icon = null;
            OnPropertyChanged(nameof(Icon));
        }
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; iconRequest?.Dispose(); iconRequest = null;
    }
}
