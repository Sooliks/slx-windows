using Slx.Client.Windows;using Xunit;
namespace Slx.Client.Tests;
public sealed class SteamMetadataTests
{
    [Theory]
    [InlineData("[InternetShortcut]\r\nURL=steam://rungameid/730","730")]
    [InlineData("[InternetShortcut]\nURL=steam://rungameid/252490/","252490")]
    public void SteamDesktopShortcutIdentifiesGameWithoutLaunching(string data,string id)=>Assert.Equal(id,SteamMetadata.AppId(data));
    [Theory]
    [InlineData("URL=https://example.com/game")]
    [InlineData("URL=steam://rungameid/730?run=extra")]
    [InlineData("URL=steam://rungameid/../730")]
    [InlineData("URL=steam://rungameid/730\nURL=steam://rungameid/252490")]
    public void UnrelatedAmbiguousOrCommandUrlsAreRejected(string data)=>Assert.Null(SteamMetadata.AppId(data));
    [Fact]
    public void SteamLibrariesUnescapeWindowsPathsAndGameFolder()
    {
        Assert.Equal(new[]{@"F:\SteamLibrary"},SteamMetadata.Values("\"libraryfolders\" { \"1\" { \"path\" \"F:\\\\SteamLibrary\" } }","path"));
        Assert.Equal(new[]{"Counter-Strike Global Offensive"},SteamMetadata.Values("\"AppState\" { \"installdir\" \"Counter-Strike Global Offensive\" }","installdir"));
    }
    [Theory]
    [InlineData("../outside")][InlineData("..")][InlineData(@"C:\outside")][InlineData(@"\\server\share")]
    public void ManifestInstallDirectoryCannotEscapeLibrary(string directory)=>Assert.False(SteamMetadata.InstallDirectory(directory));
    [Fact]
    public void TwoLibrariesResolveActualGameExecutablesAndRustHelpers()
    {
        var root=Path.Combine(Path.GetTempPath(),"SLX-Steam-"+Guid.NewGuid().ToString("N"));
        try
        {
            var second=Path.Combine(root,"second");Directory.CreateDirectory(Path.Combine(root,"steamapps"));Directory.CreateDirectory(Path.Combine(second,"steamapps"));
            File.WriteAllText(Path.Combine(root,"steamapps","libraryfolders.vdf"),"\"1\" { \"path\" \""+second.Replace("\\","\\\\",StringComparison.Ordinal)+"\" }");
            string Make(string library,string id,string folder,string relative)
            {
                File.WriteAllText(Path.Combine(library,"steamapps","appmanifest_"+id+".acf"),"\"AppState\" { \"installdir\" \""+folder+"\" }");
                var target=Path.Combine(library,"steamapps","common",folder,relative);Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.WriteAllBytes(target,[]);return target;
            }
            var cs=Make(root,"730","Counter-Strike Global Offensive",@"game\bin\win64\cs2.exe");
            Make(second,"252490","Rust","RustClient.exe");var rust=Path.Combine(second,"steamapps","common","Rust","Rust.exe");File.WriteAllBytes(rust,[]);
            var shortcuts=new[]{Path.Combine(root,"CS2.url"),Path.Combine(root,"Rust.url")};File.WriteAllText(shortcuts[0],"URL=steam://rungameid/730");File.WriteAllText(shortcuts[1],"URL=steam://rungameid/252490");
            var found=SteamApplications.Find(shortcuts,root);Assert.Equal(3,found.Count);Assert.Contains(found,x=>x.Name=="Counter-Strike 2"&&x.ExecutablePath==cs);Assert.Equal(2,found.Count(x=>x.Name=="Rust"));
        }
        finally{var resolved=Path.GetFullPath(root);var scope=Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;if(!resolved.StartsWith(scope,StringComparison.OrdinalIgnoreCase)||!Path.GetFileName(resolved).StartsWith("SLX-Steam-",StringComparison.Ordinal))throw new InvalidOperationException("TEMP_SCOPE");if(Directory.Exists(resolved))Directory.Delete(resolved,true);}
    }
}
