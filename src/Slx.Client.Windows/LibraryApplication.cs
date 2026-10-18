using System.Text.RegularExpressions;
namespace Slx.Client.Windows;
/// <summary>Automatic library discovery must not offer maintenance executables as applications.</summary>
public static partial class LibraryApplication
{
    public static bool IsCandidate(string path)
    {
        var name=Path.GetFileName(path);
        return !string.IsNullOrWhiteSpace(name)&&!Uninstaller().IsMatch(name);
    }
    [GeneratedRegex(@"\A(?:uninstall(?:er)?|uninst|unins[0-9]*|unwise(?:32)?|deinstall(?:er)?)(?:[ _.-].*|[0-9]*)?\.exe\z",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant)]
    private static partial Regex Uninstaller();
}
