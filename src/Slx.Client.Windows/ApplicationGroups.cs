using System.Security.Cryptography;
using System.Text;
using Slx.Client.Core;
using Windows.Win32;
using Windows.Win32.Foundation;
namespace Slx.Client.Windows;
/// <summary>Known OpenAI desktop family, resolved for the current user. Exact paths, never generic node/shell rules.</summary>
public static class ApplicationGroups
{
    private const string Family="OpenAI.Codex_2p2nqsd0c76g0";
    private static string Bin=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"OpenAI","Codex","bin");
    public static IReadOnlyList<AppRule> Discover(CancellationToken cancellationToken = default)
    {
        var primary=PackageExecutables(cancellationToken).FirstOrDefault(File.Exists);
        return primary is null?[]:[AppDiscovery.FromExecutable(primary) with{Name="Codex / ChatGPT"}];
    }
    public static AppRule Normalize(AppRule rule)=>Normalize(rule,CancellationToken.None);
    public static AppRule Normalize(AppRule rule,CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if(!IsGroup(rule.ExecutablePath))return rule;
        var primary=PackageExecutables(cancellationToken).FirstOrDefault(File.Exists);
        return rule with{Name="Codex / ChatGPT",ExecutablePath=primary??rule.ExecutablePath};
    }
    public static List<AppRule> Expand(IEnumerable<AppRule> applications)
    {
        var expanded=new List<AppRule>();
        foreach(var rule in applications)
        {
            expanded.Add(rule);
            if(!IsGroup(rule.ExecutablePath))continue;
            foreach(var path in PackageExecutables().Concat(Helpers()).Where(File.Exists))
            {
                var id=new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(path.ToUpperInvariant())).AsSpan(0,16));
                expanded.Add(new(id,rule.Name,path,rule.Enabled));
            }
        }
        var result=expanded.GroupBy(x=>x.ExecutablePath,StringComparer.OrdinalIgnoreCase)
            .Select(group=>group.First() with{Enabled=group.Any(x=>x.Enabled)}).ToList();
        if(result.Count>128)throw new ClientFault("INVALID_APPLICATIONS");
        return result;
    }
    private static bool IsGroup(string path)=>
        path.StartsWith(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"WindowsApps","OpenAI.Codex_"),StringComparison.OrdinalIgnoreCase)
        &&Path.GetFileName(path).Equals("ChatGPT.exe",StringComparison.OrdinalIgnoreCase)
        ||path.StartsWith(Bin+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)
        &&Path.GetFileName(path) is "codex.exe" or "codex-code-mode-host.exe";
    private static IEnumerable<string> Helpers()
    {
        if(!Directory.Exists(Bin))return [];
        return Directory.EnumerateDirectories(Bin).Where(p=>(File.GetAttributes(p)&FileAttributes.ReparsePoint)==0)
            .OrderByDescending(Directory.GetLastWriteTimeUtc).Take(20)
            .SelectMany(p=>new[]{Path.Combine(p,"codex.exe"),Path.Combine(p,"codex-code-mode-host.exe")}).ToArray();
    }
    private static unsafe IReadOnlyList<string> PackageExecutables(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if(!OperatingSystem.IsWindowsVersionAtLeast(10))return [];
        uint count=0,length=0;
        var status=PInvoke.GetPackagesByPackageFamily(Family,ref count,default,ref length,default);
        if(status!=WIN32_ERROR.ERROR_INSUFFICIENT_BUFFER||count is 0 or >32||length>262144)return [];
        var names=new PWSTR[count];var buffer=new char[length];
        fixed(char* pinned=buffer)
        {
            if(PInvoke.GetPackagesByPackageFamily(Family,ref count,names,ref length,buffer)!=WIN32_ERROR.ERROR_SUCCESS)return [];
            var paths=new List<string>();
            foreach(var name in names.Take((int)count))
            {
                cancellationToken.ThrowIfCancellationRequested();
                uint size=0;
                string full=name.ToString();
                if(PInvoke.GetPackagePathByFullName(full,ref size,default)!=WIN32_ERROR.ERROR_INSUFFICIENT_BUFFER||size>32768)continue;
                var path=new char[size];
                if(PInvoke.GetPackagePathByFullName(full,ref size,path)==WIN32_ERROR.ERROR_SUCCESS)
                    paths.Add(Path.Combine(new string(path).TrimEnd('\0'),"app","ChatGPT.exe"));
            }
            return paths;
        }
    }
}
