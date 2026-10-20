using Microsoft.Win32;
using Slx.Client.Core;
namespace Slx.Client.Windows;
public static class CatalogApplications
{
    public static IReadOnlyList<AppRule> Find(IReadOnlyList<CatalogApplication> catalog, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var found=new Dictionary<string,AppRule>(StringComparer.OrdinalIgnoreCase);
        foreach(var hive in new[]{RegistryHive.CurrentUser,RegistryHive.LocalMachine})
        foreach(var view in new[]{RegistryView.Registry64,RegistryView.Registry32})
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var registry=RegistryKey.OpenBaseKey(hive,view);
            foreach(var executable in catalog.SelectMany(x=>x.Executables).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var entry=registry.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\"+executable);
                if(entry?.GetValue("") is string path)Add(path.Trim('"'));
            }
        }
        foreach(var path in KnownLocations.Paths())Add(path);
        return found.Values.ToArray();
        void Add(string path)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if(!LibraryApplication.IsCandidate(path))return;
                var app=AppDiscovery.FromExecutable(path);
                if(catalog.Any(item=>item.Executables.Contains(Path.GetFileName(app.ExecutablePath),StringComparer.OrdinalIgnoreCase)))
                    found.TryAdd(app.ExecutablePath,app);
            }
            catch(Exception error) when(error is ClientFault or IOException or UnauthorizedAccessException or ArgumentException) { }
        }
    }
}
