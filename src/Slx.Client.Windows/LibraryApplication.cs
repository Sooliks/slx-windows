using System.Text.RegularExpressions;
namespace Slx.Client.Windows;
/// <summary>Policy for automatic discovery only; manual selection remains explicit.</summary>
public static partial class LibraryApplication
{
    public static bool IsCandidate(string path)
    {
        if(string.IsNullOrWhiteSpace(path))return false;
        var normalized=path.Replace('/', '\\');
        var cache=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"Package Cache").TrimEnd('\\');
        if(normalized.Equals(cache,StringComparison.OrdinalIgnoreCase)||normalized.StartsWith(cache+"\\",StringComparison.OrdinalIgnoreCase))return false;
        var name=Path.GetFileName(normalized);
        return !string.IsNullOrWhiteSpace(name)&&!Uninstaller().IsMatch(name);
    }
    [GeneratedRegex(@"\A(?:uninstall(?:er)?|uninst|unins[0-9]*|unwise(?:32)?|deinstall(?:er)?)(?:[ _.-].*|[0-9]*)?\.exe\z",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant)]
    private static partial Regex Uninstaller();
}
