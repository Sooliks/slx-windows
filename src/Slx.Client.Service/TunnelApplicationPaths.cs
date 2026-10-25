using Slx.Client.Core;
using Slx.Client.Windows;
namespace Slx.Client.Service;
internal sealed partial class TunnelEngine
{
    private void PrepareExactApplicationPaths(CancellationToken token)
    {
        var before = currentPlan ?? throw new ClientFault("NO_CONNECTION_PLAN");
        var prepared = ExactApplicationPlan.Prepare(before, token);
        if (ReferenceEquals(before, prepared)) return;
        currentPlan = prepared;
        if (pinnedPlan is not null) pinnedPlan = pinnedPlan with { Applications = prepared.Applications };
    }
}
