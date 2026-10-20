using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Slx.Client.Core;

/// <summary>SemVer 2 precedence; numeric components never overflow and build metadata never orders releases.</summary>
public sealed partial class ReleaseVersion : IComparable<ReleaseVersion>
{
    public string Value { get; }
    private readonly string[] numbers;
    private readonly string[] prerelease;
    public bool IsPrerelease => prerelease.Length != 0;
    private ReleaseVersion(string value, Match match)
    {
        Value = value;
        numbers = [match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value];
        prerelease = match.Groups[4].Success ? match.Groups[4].Value.Split('.') : [];
    }
    public static bool TryParse(string? value, [NotNullWhen(true)] out ReleaseVersion? version)
    {
        version = null;
        if (value is not { Length: > 0 and <= 100 }) return false;
        var match = Pattern().Match(value);
        if (!match.Success) return false;
        version = new(value, match);
        return true;
    }
    public int CompareTo(ReleaseVersion? other)
    {
        if (other is null) return 1;
        for (var index = 0; index < numbers.Length; index++)
        {
            var order = Numeric(numbers[index], other.numbers[index]);
            if (order != 0) return order;
        }
        if (!IsPrerelease || !other.IsPrerelease) return other.IsPrerelease.CompareTo(IsPrerelease);
        for (var index = 0; index < Math.Min(prerelease.Length, other.prerelease.Length); index++)
        {
            var first = prerelease[index]; var second = other.prerelease[index];
            var firstNumeric = first.All(char.IsAsciiDigit); var secondNumeric = second.All(char.IsAsciiDigit);
            var order = firstNumeric && secondNumeric ? Numeric(first, second)
                : firstNumeric != secondNumeric ? (firstNumeric ? -1 : 1) : StringComparer.Ordinal.Compare(first, second);
            if (order != 0) return order;
        }
        return prerelease.Length.CompareTo(other.prerelease.Length);
    }
    private static int Numeric(string first, string second) => first.Length == second.Length
        ? StringComparer.Ordinal.Compare(first, second) : first.Length.CompareTo(second.Length);
    [GeneratedRegex(@"\A(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-((?:0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*)(?:\.(?:0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*))*))?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?\z", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}
