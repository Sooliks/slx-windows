using Slx.Client.Core;
using Slx.Client.Windows;

namespace Slx.Client.Service;

/// <summary>Only fixed maintenance state. It never accepts an executable, path or command.</summary>
internal sealed class UpdateMaintenance(IUpdateMaintenanceStore store)
{
    private volatile ProtectedUpdateMaintenance? current;
    private volatile bool unconfirmed;
    internal Func<DateTimeOffset> UtcNow { get; init; } = () => DateTimeOffset.UtcNow;
    internal string? OwnerSid => current?.OwnerSid;
    internal bool IsUnconfirmed => unconfirmed || current is { Claimed: true } value && value.ExpiresAt <= UtcNow();
    internal UpdateMaintenanceObservation? Observation => current is { } value && (value.Claimed || value.ExpiresAt > UtcNow())
        ? new(value.SessionId, value.ExpiresAt, value.Claimed) : null;
    // A marker, even an expired/corrupt one, must never make startup blindly restore desired-on intent.
    internal bool Load()
    {
        try
        {
            current = store.LoadMaintenance();
            bool existed = current is not null;
            Refresh();
            return existed;
        }
        catch (Exception error) when (StorageError(error)) { unconfirmed = true; return true; }
    }
    internal string? BlockCode()
    {
        Refresh();
        return IsUnconfirmed ? "UPDATE_MAINTENANCE_UNCONFIRMED" : current is not null ? "UPDATE_MAINTENANCE_ACTIVE" : null;
    }
    internal bool Allows(string sid, Guid sessionId, bool administrator) => current is { } value
        && value.SessionId == sessionId && (value.OwnerSid == sid || administrator);
    internal void Prepare(string sid, Guid sessionId, bool confirmedOff)
    {
        Refresh(); RequireConfirmed();
        if (!confirmedOff) throw new ClientFault("UPDATE_CONNECTION_ACTIVE");
        if (current is not null)
        {
            if (!Allows(sid, sessionId, false)) throw new ClientFault("UPDATE_SESSION_MISMATCH");
            return;
        }
        var now = UtcNow();
        Save(new(sid, sessionId, now, now.AddMinutes(5), false));
    }
    internal void Claim(string sid, Guid sessionId, bool administrator, bool confirmedOff)
    {
        Refresh(); RequireConfirmed();
        if (!administrator) throw new ClientFault("UPDATE_ADMIN_REQUIRED");
        if (!Allows(sid, sessionId, administrator)) throw new ClientFault("UPDATE_SESSION_MISMATCH");
        if (!confirmedOff) throw new ClientFault("UPDATE_CONNECTION_ACTIVE");
        if (current!.Claimed) return;
        var now = UtcNow();
        Save(current with { Claimed = true, CreatedAt = now, ExpiresAt = now.AddMinutes(15) });
    }
    internal void Release(string sid, Guid sessionId, bool confirmedOff)
    {
        Refresh();
        // An elapsed claimed lease is not proof that the elevated installer stopped. Only the
        // authenticated owner can explicitly release it, after independently observing completion.
        if (unconfirmed) throw new ClientFault("UPDATE_MAINTENANCE_UNCONFIRMED");
        if (!confirmedOff) throw new ClientFault("UPDATE_CONNECTION_ACTIVE");
        if (current is null) return;
        if (!Allows(sid, sessionId, false)) throw new ClientFault("UPDATE_SESSION_MISMATCH");
        Save(null);
    }
    private void RequireConfirmed()
    { if (IsUnconfirmed) throw new ClientFault("UPDATE_MAINTENANCE_UNCONFIRMED"); }
    private void Refresh()
    {
        if (unconfirmed || current is not { } value) return;
        if (value.CreatedAt > UtcNow().AddMinutes(1)) { unconfirmed = true; return; }
        if (!value.Claimed && value.ExpiresAt <= UtcNow()) Save(null);
    }
    private void Save(ProtectedUpdateMaintenance? value)
    {
        try { store.SaveMaintenance(value); current = value; }
        catch (Exception error) when (StorageError(error))
        { unconfirmed = true; throw new ClientFault("UPDATE_MAINTENANCE_UNCONFIRMED"); }
    }
    private static bool StorageError(Exception error) => error is ClientFault or IOException
        or UnauthorizedAccessException or System.Security.Cryptography.CryptographicException;
    internal static UpdateMaintenance Volatile() => new(new VolatileStore());
    private sealed class VolatileStore : IUpdateMaintenanceStore
    {
        private ProtectedUpdateMaintenance? current;
        public ProtectedUpdateMaintenance? LoadMaintenance() => current;
        public void SaveMaintenance(ProtectedUpdateMaintenance? value) => current = value;
    }
}
