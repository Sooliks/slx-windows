using System.Text.RegularExpressions;

namespace Slx.Client.Core;

public static partial class PlanValidator
{
    public static void Validate(ConnectPlan? plan)
    {
        if (plan?.Profile is null || !Enum.IsDefined(plan.Mode) || plan.Applications is null
            || plan.Applications.Count > 128) throw new ClientFault("INVALID_PLAN");
        ValidateProfile(plan.Profile);
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var app in plan.Applications)
        {
            if (app is null || app.Id == Guid.Empty || string.IsNullOrWhiteSpace(app.Name)
                || app.Name.Length > 160 || app.Name.Any(char.IsControl)
                || !IsExecutablePath(app.ExecutablePath) || !paths.Add(app.ExecutablePath.Replace('/', '\\')))
                throw new ClientFault("INVALID_APPLICATION");
        }
        if (plan.Mode == RoutingMode.SelectedApplications && !plan.Applications.Any(app => app.Enabled))
            throw new ClientFault("INVALID_APPLICATION");
    }

    public static void ValidateProfile(VpnProfile? profile)
    {
        if (profile is null || string.IsNullOrWhiteSpace(profile.Id) || profile.Id.Length > 128
            || string.IsNullOrWhiteSpace(profile.Name) || profile.Name.Length > 200 || profile.Name.Any(char.IsControl)
            || profile.Port is < 1 or > 65535 || !SafeText(profile.Secret, 256))
            throw new ClientFault("INVALID_PROFILE");
        EndpointPolicy.ValidateHost(profile.Host);
        EndpointPolicy.ValidateHost(profile.ServerName);
        if (profile.Kind == "hysteria2")
        {
            if (profile.Transport is not (null or "hysteria") || profile.PublicKey is not null
                || profile.ShortId is not null || profile.Flow is not (null or "")
                || profile.Alpn is not (null or "h3") || profile.Fingerprint is not (null or "" or "unsafe"))
                throw new ClientFault("INVALID_PROFILE");
            return;
        }
        if (profile.Kind != "vless" || !Guid.TryParseExact(profile.Secret, "D", out _))
            throw new ClientFault("INVALID_PROFILE");
        if (profile.Transport == "xhttp")
        {
            if (!IsHttpPath(profile.Path) || profile.Flow is not (null or "")
                || profile.PublicKey is not null || profile.ShortId is not null
                || profile.Alpn is not (null or "h2") || profile.Fingerprint is not (null or "" or "unsafe"))
                throw new ClientFault("INVALID_PROFILE");
            return;
        }
        if (profile.Transport is not ("tcp" or "raw") || !PublicKeyPattern().IsMatch(profile.PublicKey ?? "")
            || !ShortIdPattern().IsMatch(profile.ShortId ?? "") || (profile.ShortId?.Length ?? 0) % 2 != 0
            || profile.Flow is not (null or "" or "xtls-rprx-vision")
            || profile.Fingerprint is not (null or "" or "chrome" or "firefox" or "safari" or "edge"))
            throw new ClientFault("INVALID_PROFILE");
    }

    public static bool IsExecutablePath(string? value) => value is { Length: >= 7 and <= 1024 }
        && ExecutablePattern().IsMatch(value) && !value.Contains("..", StringComparison.Ordinal)
        && !value.Any(char.IsControl) && !value.Contains('*') && !value.Contains('?')
        && !value.Contains('"') && !value.Contains('|') && !value.Contains('<') && !value.Contains('>')
        && !value[2..].Contains(':');

    private static bool SafeText(string? value, int length) => !string.IsNullOrWhiteSpace(value)
        && value.Length <= length && !value.Any(char.IsControl);

    private static bool IsHttpPath(string? value) => value is { Length: > 0 and <= 1024 }
        && value.StartsWith('/') && !value.StartsWith("//", StringComparison.Ordinal)
        && !value.Any(char.IsControl) && !value.Contains('#');

    [GeneratedRegex(@"\A[A-Za-z]:[\\/].+\.exe\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ExecutablePattern();
    [GeneratedRegex(@"\A[A-Za-z0-9_-]{43}\z", RegexOptions.CultureInvariant)]
    private static partial Regex PublicKeyPattern();
    [GeneratedRegex(@"\A[0-9a-fA-F]{0,16}\z", RegexOptions.CultureInvariant)]
    private static partial Regex ShortIdPattern();
}
