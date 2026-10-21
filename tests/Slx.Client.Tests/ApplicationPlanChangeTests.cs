using Slx.Client.Core;
using Slx.Client.Service;
using Xunit;
namespace Slx.Client.Tests;
public sealed class ApplicationPlanChangeTests
{
    [Fact]
    public void DisabledRowsOrderNamesIdsAndPathCaseCannotCauseNetworkChanges()
    {
        var enabled = CoreFixtures.App(); var other = CoreFixtures.App(@"C:\Apps\Other.exe");
        var before = CoreFixtures.Plan() with { Applications = [enabled, CoreFixtures.App(@"C:\Gone\Off.exe", false), other] };
        var after = before with { Applications = [other with { Id = Guid.NewGuid(), Name = "Renamed" },
            enabled with { Id = Guid.NewGuid(), ExecutablePath = enabled.ExecutablePath.ToUpperInvariant().Replace('\\', '/') }] };
        PlanValidator.Validate(before); PlanValidator.Validate(after);
        Assert.True(ApplicationPlanChange.SameConnection(before, after));
        Assert.True(ApplicationPlanChange.SameRoutes(before, after));
        Assert.Equal(XrayRouteRevision.For(before), XrayRouteRevision.For(after));
    }
    [Fact]
    public void NewEnabledPathIsARealChangeButWholeModeIgnoresApplicationRows()
    {
        var before = CoreFixtures.Plan();
        var changed = before with { Applications = [..before.Applications, CoreFixtures.App(@"C:\Apps\New.exe")] };
        Assert.False(ApplicationPlanChange.SameRoutes(before, changed));
        Assert.True(ApplicationPlanChange.SameConnection(before, changed));
        Assert.True(ApplicationPlanChange.SameRoutes(before with { Mode = RoutingMode.WholeComputer },
            changed with { Mode = RoutingMode.WholeComputer }));
    }
    [Fact]
    public void MatchingDisplayNameDoesNotAuthorizeChangingEndpointCredentialsOrMode()
    {
        var before = CoreFixtures.Plan();
        Assert.False(ApplicationPlanChange.SameConnection(before, before with { Profile = before.Profile with { Port = 8443 } }));
        Assert.False(ApplicationPlanChange.SameConnection(before, before with { Profile = before.Profile with { Secret = "anotherSecret" } }));
        Assert.False(ApplicationPlanChange.SameConnection(before, before with { Mode = RoutingMode.WholeComputer }));
    }
}
