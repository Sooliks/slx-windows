using Slx.Client.Core;
namespace Slx.Client.Windows;
/// <summary>One exact, native-identity-checked application list for WFP, Xray and durable desired intent.</summary>
public static class ExactApplicationPlan
{
    public const string IdentityMismatch = "TRAFFIC_APPLICATION_IDENTITY_MISMATCH";
    public static ConnectPlan Prepare(ConnectPlan plan, CancellationToken token = default) =>
        Prepare(plan, ExactExecutablePath.Open, path => TrafficApplicationIds.Read(path, true), token);
    public static ProtectedTunnelIntent Prepare(ProtectedTunnelIntent intent, CancellationToken token = default)
    {
        var plan = Prepare(intent.Plan, token);
        return ReferenceEquals(plan, intent.Plan) ? intent : intent with { Plan = plan };
    }
    internal static ConnectPlan Prepare(ConnectPlan plan, Func<string, CancellationToken, ExactExecutablePath> open,
        Func<string, byte[]> readIdentity, CancellationToken token = default)
    {
        PlanValidator.Validate(plan);
        token.ThrowIfCancellationRequested();
        if (plan.Mode != RoutingMode.SelectedApplications) return plan;
        var applications = new List<AppRule>();
        bool changed = false;
        foreach (var app in plan.Applications)
        {
            token.ThrowIfCancellationRequested();
            if (!app.Enabled) { applications.Add(app); continue; }
            // Handles hold every ancestor against rename and reject reparses before either path-based ID lookup.
            using var file = open(app.ExecutablePath, token);
            token.ThrowIfCancellationRequested();
            if (!PlanValidator.IsExecutablePath(file.Path)
                || !app.ExecutablePath.Replace('/', '\\').Equals(file.Path, StringComparison.OrdinalIgnoreCase))
                throw new ClientFault(ExactExecutablePath.Unsupported);
            var original = readIdentity(app.ExecutablePath);
            token.ThrowIfCancellationRequested();
            var canonical = readIdentity(file.Path);
            if (original.Length is 0 or >65536 || canonical.Length is 0 or >65536
                || !original.AsSpan().SequenceEqual(canonical)) throw new ClientFault(IdentityMismatch);
            token.ThrowIfCancellationRequested();
            changed |= !app.ExecutablePath.Equals(file.Path, StringComparison.Ordinal);
            applications.Add(app with { ExecutablePath = file.Path });
        }
        token.ThrowIfCancellationRequested();
        if (!changed) return plan;
        var prepared = plan with { Applications = applications };
        PlanValidator.Validate(prepared);
        return prepared;
    }
}
