using Slx.Client.Core;

namespace Slx.Client.Windows;

/// <summary>Shared Windows DNS must remain available to unselected applications during VPN recovery.</summary>
internal static class TrafficDnsPolicy
{
    internal static bool BlockSharedDns(RoutingMode mode) => mode switch
    {
        RoutingMode.WholeComputer => true,
        RoutingMode.SelectedApplications => false,
        _ => throw new ClientFault("INVALID_PLAN")
    };
}
internal enum TrafficProtectionRuleKind { SharedDnsBlock, GlobalBlock, Application }
internal sealed record TrafficProtectionRule(TrafficProtectionRuleKind Kind, TrafficApplicationId? Application = null, Guid? Key = null);

/// <summary>The exact mode-dependent rules consumed by the native full-replacement transaction.</summary>
internal static class TrafficProtectionPolicy
{
    internal static IReadOnlyList<TrafficProtectionRule> Build(RoutingMode mode, IReadOnlyList<TrafficApplicationId> applications,
        TrafficApplicationState? selected, bool ipv4)
    {
        var rules = new List<TrafficProtectionRule>();
        if (TrafficDnsPolicy.BlockSharedDns(mode)) rules.Add(new(TrafficProtectionRuleKind.SharedDnsBlock));
        if (mode == RoutingMode.WholeComputer)
        {
            foreach (var application in applications) rules.Add(new(TrafficProtectionRuleKind.Application, application));
            rules.Add(new(TrafficProtectionRuleKind.GlobalBlock));
        }
        else
        {
            if (selected is null) throw new ClientFault("TRAFFIC_PROTECTION_APPLICATION_BASELINE");
            foreach (var application in selected.Filters.Values)
                rules.Add(new(TrafficProtectionRuleKind.Application, new(application.Identity, false), ipv4 ? application.V4 : application.V6));
        }
        return rules;
    }
}
