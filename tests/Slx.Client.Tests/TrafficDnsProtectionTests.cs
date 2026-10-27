using Slx.Client.Core;
using Slx.Client.Windows;
using Xunit;

namespace Slx.Client.Tests;

/// <summary>Actual native-filter construction policy; does not open or change Windows Filtering Platform.</summary>
public sealed class TrafficDnsProtectionTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SelectedProtectionBlocksOnlyEnabledExactApplicationsAndNeverSharedDns(bool ipv4)
    {
        var plan = CoreFixtures.Plan(); var enabled = plan.Applications[0];
        plan = plan with { Applications = [enabled, enabled with { Id = Guid.NewGuid(), Enabled = false, ExecutablePath = @"C:\Disabled\other.exe" }] };
        var ids = TrafficApplicationIds.Resolve(plan, "core", "service", (path, selected) =>
        { Assert.Equal(enabled.ExecutablePath, path); Assert.True(selected); return [1, 2, 3]; });
        var state = new TrafficApplicationState(TrafficApplicationPolicy.FromIdentities(ids.Select(id => id.Identity)));
        var rules = TrafficProtectionPolicy.Build(plan.Mode, ids, state, ipv4);
        var application = Assert.Single(rules);
        Assert.Equal(TrafficProtectionRuleKind.Application, application.Kind);
        Assert.False(application.Application!.Permit); Assert.Equal(new byte[] { 1, 2, 3 }, application.Application.Identity);
        var filter = Assert.Single(state.Filters.Values); Assert.Equal(ipv4 ? filter.V4 : filter.V6, application.Key);
        Assert.DoesNotContain(rules, rule => rule.Kind is TrafficProtectionRuleKind.SharedDnsBlock or TrafficProtectionRuleKind.GlobalBlock);
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void WholeComputerRetainsGlobalDnsAndPublicBlocksWithOnlyOwnedRecoveryPermits(bool ipv4)
    {
        var plan = CoreFixtures.Plan(RoutingMode.WholeComputer); var calls = new List<string>();
        var ids = TrafficApplicationIds.Resolve(plan, "core", "service", (path, selected) =>
        { Assert.False(selected); calls.Add(path); return path == "core" ? [4] : [5]; });
        var rules = TrafficProtectionPolicy.Build(plan.Mode, ids, null, ipv4);
        Assert.Equal(new[] { "core", "service" }, calls);
        Assert.Single(rules, rule => rule.Kind == TrafficProtectionRuleKind.SharedDnsBlock);
        Assert.Single(rules, rule => rule.Kind == TrafficProtectionRuleKind.GlobalBlock);
        var permits = rules.Where(rule => rule.Kind == TrafficProtectionRuleKind.Application).ToArray(); Assert.Equal(2, permits.Length);
        Assert.All(permits, rule => { Assert.True(rule.Application!.Permit); Assert.Null(rule.Key); });
        Assert.Equal(new byte[] { 4 }, permits[0].Application!.Identity); Assert.Equal(new byte[] { 5 }, permits[1].Application!.Identity);
    }
    [Fact]
    public void FullModeReplacementCannotCarryWholeComputerGlobalDeniesIntoSelectedRules()
    {
        var before = TrafficProtectionPolicy.Build(RoutingMode.WholeComputer, [new([9], true), new([8], true)], null, true);
        var state = new TrafficApplicationState(TrafficApplicationPolicy.FromIdentities([[7]]));
        var after = TrafficProtectionPolicy.Build(RoutingMode.SelectedApplications, [new([7], false)], state, true);
        Assert.Contains(before, rule => rule.Kind == TrafficProtectionRuleKind.GlobalBlock);
        Assert.DoesNotContain(after, rule => rule.Kind != TrafficProtectionRuleKind.Application || rule.Application!.Permit);
        Assert.Equal(new byte[] { 7 }, Assert.Single(after).Application!.Identity);
        // A later whole-PC replacement still emits all whole-PC protection, independent of the previous selected snapshot.
        Assert.Equal(before.Select(rule => rule.Kind), TrafficProtectionPolicy.Build(RoutingMode.WholeComputer,
            [new([9], true), new([8], true)], null, true).Select(rule => rule.Kind));
    }
    [Fact]
    public void InvalidModeOrMissingSelectedIdentityBaselineCannotProduceAnOpenPolicy()
    {
        Assert.Throws<ClientFault>(() => TrafficProtectionPolicy.Build((RoutingMode)999, [], null, true));
        Assert.Throws<ClientFault>(() => TrafficProtectionPolicy.Build(RoutingMode.SelectedApplications, [], null, true));
    }
}
