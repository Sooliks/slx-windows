using Slx.Client.Core;
namespace Slx.Client.Windows;

/// <summary>Stored preferences are local intent, not proof that an executable is still installed.</summary>
internal static class PreferencesData
{
    public static ClientPreferences Validate(ClientPreferences? value)
    {
        if (value is null || value.Language is not ("ru" or "en") || value.Theme is not ("olive" or "terminal")
            || !Enum.IsDefined(value.Mode) || value.Applications?.Count > 128
            || value.SelectedProfileId is {} profile && (profile.Length > 128 || profile.Any(char.IsControl)))
            throw new ClientFault("STORAGE_UNAVAILABLE");
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var app in value.Applications ?? [])
        {
            if (app is null || app.Id == Guid.Empty || string.IsNullOrWhiteSpace(app.Name)
                || app.Name.Length > 160 || app.Name.Any(char.IsControl)
                || !PlanValidator.IsExecutablePath(app.ExecutablePath)
                || !paths.Add(app.ExecutablePath.Replace('/', '\\')))
                throw new ClientFault("STORAGE_UNAVAILABLE");
        }
        return value;
    }
}
