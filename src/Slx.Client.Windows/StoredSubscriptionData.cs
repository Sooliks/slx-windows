using Slx.Client.Core;
namespace Slx.Client.Windows;

/// <summary>Revalidate the typed decrypted data before the desktop consumes it. No request or address resolution.</summary>
internal static class StoredSubscriptionData
{
    public static void Validate(StoredSubscription? value)
    {
        if (value is null) return;
        try
        {
            SubscriptionClient.ValidateUrl(value.Url);
            var snapshot = value.Snapshot;
            if (snapshot is null || string.IsNullOrWhiteSpace(snapshot.Title) || snapshot.Title.Length > 160
                || snapshot.Title.Any(char.IsControl) || snapshot.Profiles is not { Count: > 0 and <= 64 })
                throw new ClientFault("STORAGE_UNAVAILABLE");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var profile in snapshot.Profiles)
            {
                PlanValidator.ValidateProfile(profile);
                if (!ids.Add(profile.Id)) throw new ClientFault("STORAGE_UNAVAILABLE");
            }
        }
        catch (ClientFault) { throw new ClientFault("STORAGE_UNAVAILABLE"); }
    }
}
