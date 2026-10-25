using Slx.Client.Core;
using Slx.Client.Windows;
using Xunit;
namespace Slx.Client.Tests;
public sealed class ExactApplicationPlanTests
{
    [Fact]
    public void CasingPreparationUsesOneListAndPreservesIdentityChoiceProfileAndRevision()
    {
        var original = CoreFixtures.Plan() with { Applications = [new(Guid.NewGuid(), "Chosen", @"c:\apps\chosen.exe")] };
        var canonical = @"C:\Apps\Chosen.exe";
        var lookups = new List<string>();
        var prepared = ExactApplicationPlan.Prepare(original, (_, _) => new(canonical), path => { lookups.Add(path); return [1, 2]; });
        Assert.NotSame(original, prepared); Assert.Same(original.Profile, prepared.Profile); Assert.Equal(original.Mode, prepared.Mode);
        var before = Assert.Single(original.Applications); var after = Assert.Single(prepared.Applications);
        Assert.Equal(before.Id, after.Id); Assert.Equal(before.Name, after.Name); Assert.Equal(before.Enabled, after.Enabled);
        Assert.Equal(canonical, after.ExecutablePath); Assert.Equal(new[] { before.ExecutablePath, canonical }, lookups);
        Assert.Equal(XrayRouteRevision.For(original), XrayRouteRevision.For(prepared));
        Assert.True(Slx.Client.Service.ApplicationPlanChange.SameRoutes(original, prepared));
        using var json = System.Text.Json.JsonDocument.Parse(XrayConfigBuilder.Build(prepared, 18899, "probeUser00000000", "probePassword0000"));
        Assert.Contains(json.RootElement.GetProperty("routing").GetProperty("rules").EnumerateArray(), rule =>
            rule.TryGetProperty("process", out var paths) && paths.EnumerateArray().Any(path => path.GetString() == canonical.Replace('\\', '/')));
    }
    [Fact]
    public void DisabledAndWholeComputerPathsAreNotOpenedOrRemapped()
    {
        var plan = CoreFixtures.Plan(); var disabled = plan.Applications[0] with { Id = Guid.NewGuid(), Enabled = false,
            ExecutablePath = @"C:\Removed\Disabled.exe" };
        plan = plan with { Applications = [plan.Applications[0], disabled] };
        var prepared = ExactApplicationPlan.Prepare(plan, (path, _) =>
        { Assert.NotEqual(disabled.ExecutablePath, path); return new(path); }, _ => [1]);
        Assert.Same(plan, prepared); Assert.Same(disabled, prepared.Applications[1]);
        var whole = plan with { Mode = RoutingMode.WholeComputer };
        Assert.Same(whole, ExactApplicationPlan.Prepare(whole, (_, _) => throw new InvalidOperationException(), _ => throw new InvalidOperationException()));
    }
    [Fact]
    public void MismatchedNativeIdentityNeverProducesAPreparedPlan()
    {
        var plan = CoreFixtures.Plan(); var original = Assert.Single(plan.Applications); int reads = 0;
        var error = Assert.Throws<ClientFault>(() => ExactApplicationPlan.Prepare(plan,
            (path, _) => new(path.ToUpperInvariant()), _ => [checked((byte)++reads)]));
        Assert.Equal(ExactApplicationPlan.IdentityMismatch, error.Code); Assert.Equal(2, reads);
        Assert.Same(original, Assert.Single(plan.Applications));
    }
    [Theory]
    [InlineData(@"C:\Other\app.exe")]
    [InlineData(@"\\remote\share\app.exe")]
    [InlineData(@"C:\Apps\app.dll")]
    public void AliasOrInvalidCanonicalPathIsRejectedBeforeNativeIdentityRead(string canonical)
    {
        var error = Assert.Throws<ClientFault>(() => ExactApplicationPlan.Prepare(CoreFixtures.Plan(),
            (_, _) => new(canonical), _ => throw new InvalidOperationException()));
        Assert.Equal(ExactExecutablePath.Unsupported, error.Code);
    }
    [Fact]
    public void MissingFileFailureAndCancellationLeaveTheOriginalIntentUntouched()
    {
        var plan = CoreFixtures.Plan(); var original = Assert.Single(plan.Applications);
        Assert.Equal("TRAFFIC_APPLICATION_MISSING", Assert.Throws<ClientFault>(() => ExactApplicationPlan.Prepare(plan,
            (_, _) => throw new ClientFault("TRAFFIC_APPLICATION_MISSING"), _ => throw new InvalidOperationException())).Code);
        using var before = new CancellationTokenSource(); before.Cancel();
        Assert.Throws<OperationCanceledException>(() => ExactApplicationPlan.Prepare(plan,
            (_, _) => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), before.Token));
        using var during = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => ExactApplicationPlan.Prepare(plan,
            (path, token) => { Assert.Equal(during.Token, token); during.Cancel(); return new(path); },
            _ => throw new InvalidOperationException(), during.Token));
        Assert.Same(original, Assert.Single(plan.Applications));
    }
}
