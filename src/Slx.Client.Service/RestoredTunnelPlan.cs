using Slx.Client.Core;
using Slx.Client.Windows;
namespace Slx.Client.Service;
/// <summary>Prepares the saved on-intent without changing the endpoint, owner, storage or network policy.</summary>
internal static class RestoredTunnelPlan
{
    public static ProtectedTunnelIntent Refresh(ProtectedTunnelIntent saved, CancellationToken token = default)
    {
        PlanValidator.Validate(saved.Plan);
        token.ThrowIfCancellationRequested();
        var applications=saved.Plan.Mode == RoutingMode.SelectedApplications
            ? VersionedApplicationPaths.RefreshProtected(saved.Plan.Applications, token) : saved.Plan.Applications;
        token.ThrowIfCancellationRequested();
        if(applications.SequenceEqual(saved.Plan.Applications))return saved;
        var repaired=saved with{Plan=saved.Plan with{Applications=applications.ToList()}};
        PlanValidator.Validate(repaired.Plan);
        return repaired;
    }
}
