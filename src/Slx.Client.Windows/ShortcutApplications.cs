using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using Windows.Win32.UI.Shell;
using Slx.Client.Core;
namespace Slx.Client.Windows;
/// <summary>Reads local shortcuts without Resolve, shell execution, arguments or network searches.</summary>
public static class ShortcutApplications
{
    public static IReadOnlyList<AppRule> Find()
    {
        var found=new Dictionary<string,AppRule>(StringComparer.OrdinalIgnoreCase);
        var roots=new[]{Environment.SpecialFolder.DesktopDirectory,Environment.SpecialFolder.CommonDesktopDirectory,
            Environment.SpecialFolder.StartMenu,Environment.SpecialFolder.CommonStartMenu};
        var queue=new Queue<(string Path,int Depth)>(roots.Select(x=>(Environment.GetFolderPath(x),0)));
        int visited=0;
        while(queue.TryDequeue(out var item)&&visited<2000)
        {
            try
            {
                if(!Directory.Exists(item.Path)||(File.GetAttributes(item.Path)&FileAttributes.ReparsePoint)!=0)continue;
                foreach(var file in Directory.EnumerateFiles(item.Path,"*.lnk").Take(2000-visited))
                {
                    visited++;
                    try
                    {
                        var path=ReadTarget(file);
                        if(path is null||!LibraryApplication.IsCandidate(path))continue;
                        var rule=AppDiscovery.FromExecutable(path);
                        var name=Path.GetFileNameWithoutExtension(file);
                        if(name.Length is >0 and <=100)rule=rule with{Name=name};
                        found.TryAdd(rule.ExecutablePath,rule);
                    }
                    catch(Exception error) when(error is COMException or ClientFault or IOException or UnauthorizedAccessException or ArgumentException) { }
                }
                if(item.Depth<5)foreach(var directory in Directory.EnumerateDirectories(item.Path).Take(200))queue.Enqueue((directory,item.Depth+1));
            }
            catch(Exception error) when(error is IOException or UnauthorizedAccessException) { }
        }
        return found.Values.Take(300).ToArray();
    }
    private static unsafe string? ReadTarget(string shortcut)
    {
        object instance=Activator.CreateInstance(Type.GetTypeFromCLSID(new("00021401-0000-0000-C000-000000000046"),true)!)!;
        try
        {
            ((IPersistFile)instance).Load(shortcut,0);
            var link=(IShellLinkW)instance;
            var buffer=new char[32768];
            fixed(char* target=buffer)link.GetPath(target,buffer.Length,null,4); // SLGP_RAWPATH; never Resolve.
            var path=new string(buffer).TrimEnd('\0');
            if(string.IsNullOrWhiteSpace(path))return null;
            path=Environment.ExpandEnvironmentVariables(path);
            return path.StartsWith(@"\\",StringComparison.Ordinal)?null:path;
        }
        finally { Marshal.FinalReleaseComObject(instance); }
    }
}
