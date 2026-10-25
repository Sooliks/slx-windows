using Slx.Client.Core;
using Slx.Client.Windows;
namespace Slx.Client.Service;
/// <summary>Prepare one fully validated replacement list before recovery adopts any new intent.</summary>
internal static class RecoveredApplicationPlan
{
    internal static ConnectPlan Prepare(ConnectPlan plan, CancellationToken token,
        Func<ConnectPlan, CancellationToken, ConnectPlan>? canonicalize = null)
    {
        PlanValidator.Validate(plan);
        token.ThrowIfCancellationRequested();
        if (plan.Mode != RoutingMode.SelectedApplications) return plan;
        var applications = VersionedApplicationPaths.RefreshProtected(plan.Applications, token);
        var proposed = applications.SequenceEqual(plan.Applications) ? plan : plan with { Applications = applications.ToList() };
        PlanValidator.Validate(proposed);
        var prepared = canonicalize is null ? ExactApplicationPlan.Prepare(proposed, token) : canonicalize(proposed, token);
        token.ThrowIfCancellationRequested();
        return prepared;
    }
}
