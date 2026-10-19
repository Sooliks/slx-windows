using System.Text.RegularExpressions;
namespace Slx.Client.Windows;
/// <summary>Bounded local Steam metadata parser. Never launches a URL, command, game or Steam client.</summary>
public static partial class SteamMetadata
{
    public static string? AppId(string text)
    {
        if(text.Length>16384)return null;
        var urls=text.Split('\n').Select(x=>x.Trim()).Where(x=>x.StartsWith("URL=",StringComparison.OrdinalIgnoreCase)).ToArray();
        if(urls.Length!=1)return null;
        var match=SteamUrl().Match(urls[0][4..]);return match.Success?match.Groups[1].Value:null;
    }
    public static IReadOnlyList<string> Values(string text,string key)
    {
        if(text.Length>1048576||key is not("path" or "installdir"))return [];
        return Pair().Matches(text).Where(x=>x.Groups[1].Value.Equals(key,StringComparison.OrdinalIgnoreCase))
            .Select(x=>x.Groups[2].Value.Replace("\\\\","\\",StringComparison.Ordinal).Replace("\\\"","\"",StringComparison.Ordinal)).ToArray();
    }
    public static bool InstallDirectory(string value)=>value.Length is >0 and <=200&&value is not("." or "..")
        &&value.IndexOfAny(Path.GetInvalidFileNameChars())<0&&!value.Any(char.IsControl);
    [GeneratedRegex(@"\Asteam://rungameid/([1-9][0-9]{0,9})/?\z",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,100)]
    private static partial Regex SteamUrl();
    [GeneratedRegex("\"([^\"\\\\]*)\"\\s*\"((?:\\\\.|[^\"\\\\])*)\"",RegexOptions.CultureInvariant,100)]
    private static partial Regex Pair();
}
