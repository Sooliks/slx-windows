namespace Slx.Client.Core;

public enum RoutingMode { SelectedApplications, WholeComputer }
public sealed record AppRule(Guid Id, string Name, string ExecutablePath, bool Enabled = true)
{
    public override string ToString() => "AppRule { Private application data = [redacted] }";
}
public sealed record VpnProfile(string Id, string Name, string Kind, string Host, int Port,
    string Secret, string ServerName, string? PublicKey = null, string? ShortId = null,
    string? Transport = null, string? Path = null, string? Flow = null,
    string? Fingerprint = null, string? Alpn = null)
{
    public override string ToString() => "VpnProfile { Sensitive profile data = [redacted] }";
}
public sealed record SubscriptionSnapshot(string Title, DateTimeOffset? ExpiresAt,
    IReadOnlyList<VpnProfile> Profiles, DateTimeOffset FetchedAt, string? Notice = null)
{
    public override string ToString() => "SubscriptionSnapshot { Private subscription data = [redacted] }";
}
public sealed record StoredSubscription(string Url, SubscriptionSnapshot Snapshot)
{
    public override string ToString() => "StoredSubscription { Url = [redacted], Snapshot = [redacted] }";
}
public sealed record ClientPreferences(string Language = "ru", string Theme = "olive",
    RoutingMode Mode = RoutingMode.SelectedApplications, List<AppRule>? Applications = null,
    bool MinimizeToTray = true, bool AutoStart = false, string? SelectedProfileId = null,
    bool CheckUpdatesAutomatically = true, DateTimeOffset? UpdateReminderUntil = null)
{
    public override string ToString() => "ClientPreferences { Private settings = [redacted] }";
}
public sealed record ConnectPlan(VpnProfile Profile, RoutingMode Mode, List<AppRule> Applications)
{
    public override string ToString() => "ConnectPlan { Profile = [redacted], Applications = [redacted] }";
}
public sealed class ClientFault(string code) : Exception(code) { public string Code { get; } = code; }
