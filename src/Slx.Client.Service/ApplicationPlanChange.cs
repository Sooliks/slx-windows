using Slx.Client.Core;
namespace Slx.Client.Service;
internal static class ApplicationPlanChange
{
    public static bool SameConnection(ConnectPlan before, ConnectPlan after) =>
        before.Mode == after.Mode && before.Profile == after.Profile;
    public static bool SameRoutes(ConnectPlan before, ConnectPlan after) => before.Mode == after.Mode
        && (before.Mode == RoutingMode.WholeComputer || Paths(before).SetEquals(Paths(after)));
    private static HashSet<string> Paths(ConnectPlan plan) => plan.Applications.Where(app => app.Enabled)
        .Select(app => app.ExecutablePath.Replace('/', '\\')).ToHashSet(StringComparer.OrdinalIgnoreCase);
}
