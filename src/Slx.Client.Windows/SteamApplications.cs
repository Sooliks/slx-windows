using Microsoft.Win32;
using System.Text.RegularExpressions;
using Slx.Client.Core;
namespace Slx.Client.Windows;
public static class SteamApplications
{
    public static IReadOnlyList<AppRule> Find(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var key=Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
        var root=key?.GetValue("SteamPath") as string??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"Steam");
        var shortcuts=new List<string>();
        foreach(var folder in new[]{Environment.SpecialFolder.DesktopDirectory,Environment.SpecialFolder.CommonDesktopDirectory})
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directory=Environment.GetFolderPath(folder);
            try{if(Directory.Exists(directory))shortcuts.AddRange(Directory.EnumerateFiles(directory,"*.url").Take(500));}
            catch(Exception error) when(error is IOException or UnauthorizedAccessException) { }
        }
        return Find(shortcuts,root,cancellationToken);
    }
    public static IReadOnlyList<AppRule> Find(IEnumerable<string> shortcuts,string steamRoot,CancellationToken cancellationToken = default)
    {
        var found=new Dictionary<string,AppRule>(StringComparer.OrdinalIgnoreCase);
        var requested=new HashSet<string>(StringComparer.Ordinal);
        foreach(var shortcut in shortcuts.Take(1000))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try{var id=SteamMetadata.AppId(Read(shortcut,16384));if(id is not null)requested.Add(id);}
            catch(Exception error) when(error is IOException or UnauthorizedAccessException or ArgumentException or RegexMatchTimeoutException) { }
        }
        if(requested.Count==0||!LocalDirectory(steamRoot))return [];
        var libraries=new HashSet<string>(StringComparer.OrdinalIgnoreCase){Path.GetFullPath(steamRoot)};
        try{foreach(var path in SteamMetadata.Values(Read(Path.Combine(steamRoot,"steamapps","libraryfolders.vdf"),1048576),"path").Take(64))if(LocalDirectory(path))libraries.Add(Path.GetFullPath(path));}
        catch(Exception error) when(error is IOException or UnauthorizedAccessException or ArgumentException or RegexMatchTimeoutException) { }
        foreach(var id in requested)
        {
            cancellationToken.ThrowIfCancellationRequested();
            (string Name,string[] Executables)? known=id switch
            {
                "730"=>("Counter-Strike 2",[@"game\bin\win64\cs2.exe"]),
                "252490"=>("Rust",["RustClient.exe","Rust.exe"]),
                _=>null
            };
            if(known is null)continue;
            foreach(var library in libraries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var values=SteamMetadata.Values(Read(Path.Combine(library,"steamapps","appmanifest_"+id+".acf"),262144),"installdir");
                    if(values.Count!=1||!SteamMetadata.InstallDirectory(values[0]))continue;
                    var gameRoot=Path.GetFullPath(Path.Combine(library,"steamapps","common",values[0]))+Path.DirectorySeparatorChar;
                    foreach(var relative in known.Value.Executables)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var executable=Path.GetFullPath(Path.Combine(gameRoot,relative));
                        if(!executable.StartsWith(gameRoot,StringComparison.OrdinalIgnoreCase)||!LibraryApplication.IsCandidate(executable)||!File.Exists(executable))continue;
                        found.TryAdd(executable,AppDiscovery.FromExecutable(executable) with{Name=known.Value.Name});
                    }
                }
                catch(Exception error) when(error is ClientFault or IOException or UnauthorizedAccessException or ArgumentException or RegexMatchTimeoutException) { }
            }
        }
        return found.Values.ToArray();
    }
    private static bool LocalDirectory(string path)=>Path.IsPathFullyQualified(path)&&!path.StartsWith(@"\\",StringComparison.Ordinal)&&Directory.Exists(path);
    private static string Read(string path,int limit)
    {
        var file=new FileInfo(path);if(!file.Exists||file.Length>limit||(file.Attributes&FileAttributes.ReparsePoint)!=0)throw new IOException();
        return File.ReadAllText(path);
    }
}
