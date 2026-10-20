using System.Globalization;
using System.Text;
using Slx.Client.Core;

namespace Slx.Client.Windows;

public sealed record ApplicationDiscoverySnapshot(DateTimeOffset CapturedAt, IReadOnlyList<AppRule> Applications)
{
    public override string ToString() => "ApplicationDiscoverySnapshot { Private application data = [redacted] }";
}

internal static class DiscoveryCacheValidation
{
    public static void Validate(ApplicationDiscoverySnapshot snapshot)
    {
        if (snapshot is null || snapshot.Applications is null || snapshot.Applications.Count > 300
            || snapshot.CapturedAt.Offset != TimeSpan.Zero || snapshot.CapturedAt <= DateTimeOffset.UnixEpoch
            || snapshot.CapturedAt > DateTimeOffset.UtcNow.AddMinutes(5)) Fail();
        var ids = new HashSet<Guid>();
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var app in snapshot.Applications)
        {
            if (app is null || app.Id == Guid.Empty || !ids.Add(app.Id) || !ValidName(app.Name)
                || !ValidPath(app.ExecutablePath) || !paths.Add(app.ExecutablePath)) Fail();
        }
    }

    private static bool ValidName(string? value) => value is { Length: > 0 and <= 100 }
        && !string.IsNullOrWhiteSpace(value) && value == value.Trim() && SafeText(value);

    private static bool ValidPath(string? value)
    {
        if (!PlanValidator.IsExecutablePath(value) || value!.Contains('/') || !SafeText(value)) return false;
        var segments = value[3..].Split('\\');
        if (segments.Any(segment => string.IsNullOrWhiteSpace(segment) || segment.EndsWith('.') || segment.EndsWith(' ')
            || segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || DeviceName(segment))) return false;
        return !Path.GetFileName(value).Contains(".exe ", StringComparison.OrdinalIgnoreCase)
            && Path.IsPathFullyQualified(value) && string.Equals(Path.GetFullPath(value), value, StringComparison.OrdinalIgnoreCase);
    }

    private static bool DeviceName(string segment)
    {
        var name = segment.Split('.')[0];
        return name.Equals("CON", StringComparison.OrdinalIgnoreCase) || name.Equals("PRN", StringComparison.OrdinalIgnoreCase)
            || name.Equals("AUX", StringComparison.OrdinalIgnoreCase) || name.Equals("NUL", StringComparison.OrdinalIgnoreCase)
            || name.Length == 4 && (name.StartsWith("COM", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) && name[3] is >= '1' and <= '9';
    }

    private static bool SafeText(string value)
    {
        try { _ = new UTF8Encoding(false, true).GetByteCount(value); }
        catch (EncoderFallbackException) { return false; }
        return value.EnumerateRunes().All(rune => Rune.GetUnicodeCategory(rune) is not
            (UnicodeCategory.Control or UnicodeCategory.Format or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator));
    }

    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    private static void Fail() => throw new ClientFault("STORAGE_UNAVAILABLE");
}
