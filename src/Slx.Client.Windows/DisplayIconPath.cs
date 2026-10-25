using System.Globalization;
namespace Slx.Client.Windows;
/// <summary>Reads an executable icon location without treating a comma inside its path as an icon index.</summary>
internal static class DisplayIconPath
{
    public static string Parse(string location)
    {
        var value = location.Trim();
        int comma = value.LastIndexOf(',');
        if (comma > 0 && OutsideQuotes(value, comma)
            && int.TryParse(value.AsSpan(comma + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            value = value[..comma].Trim();
        return Environment.ExpandEnvironmentVariables(value.Trim('"'));
    }
    private static bool OutsideQuotes(string value, int position)
    {
        bool quoted = false;
        for (int index = 0; index < position; index++)
            if (value[index] == '"') quoted = !quoted;
        return !quoted;
    }
}
