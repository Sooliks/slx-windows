using Slx.Client.Core;
using Slx.Client.Service;
using Slx.Client.Windows;
using Xunit;
namespace Slx.Client.Tests;
/// <summary>Actual local path preparation; injected adoption boundary, never service/WFP/network mutations.</summary>
public sealed class RecoveredApplicationPlanTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "slx-recovery-path-" + Guid.NewGuid().ToString("N"));
    [Theory]
    [InlineData("Discord", "app-old", "app-new", "Discord.exe")]
    [InlineData(@"Roblox\Versions", "version-old", "version-new", "RobloxPlayerBeta.exe")]
    public void MissingVersionIsRefreshedBeforeOneCompleteNativePreparation(string family, string oldVersion,
        string newVersion, string executable)
    {
        string previous = Path.Combine(root, family, oldVersion, executable);
        string current = Create(family, newVersion, executable);
        var original = Plan(previous); int preparations = 0;
        var prepared = RecoveredApplicationPlan.Prepare(original, default, (candidate, token) =>
        {
            preparations++;
            Assert.Equal(current, Assert.Single(candidate.Applications).ExecutablePath);
            return ExactApplicationPlan.Prepare(candidate, token);
        });
        Assert.Equal(1, preparations); Assert.Equal(current, Assert.Single(prepared.Applications).ExecutablePath);
        Assert.Equal(previous, Assert.Single(original.Applications).ExecutablePath);
        Assert.Equal(original.Applications[0].Id, prepared.Applications[0].Id);
        Assert.Same(original.Profile, prepared.Profile); Assert.Equal(original.Mode, prepared.Mode);
        Assert.NotEqual(XrayRouteRevision.For(original), XrayRouteRevision.For(prepared));
    }
    [Fact]
    public void DisabledRulesStayUnchangedAndCannotBeEnabledBySiblingExpansion()
    {
        string old = Create("Discord", "app-old", "Discord.exe");
        string explicitlyDisabled = Create("Discord", "app-new", "Discord.exe");
        var disabled = CoreFixtures.App(explicitlyDisabled, false);
        var unrelated = CoreFixtures.App(Path.Combine(root, "Removed", "Optional.exe"), false);
        var original = Plan(old) with { Applications = [CoreFixtures.App(old), disabled, unrelated] };
        var prepared = RecoveredApplicationPlan.Prepare(original, default);
        Assert.Equal(3, prepared.Applications.Count);
        Assert.Same(disabled, prepared.Applications[1]); Assert.Same(unrelated, prepared.Applications[2]);
        Assert.Single(prepared.Applications, app => app.Enabled);
    }
    [Fact]
    public void MissingEnabledRuleIsNotReplacedWithExplicitlyDisabledNewVersion()
    {
        string old = Path.Combine(root, "Discord", "app-old", "Discord.exe");
        string current = Create("Discord", "app-new", "Discord.exe");
        var original = Plan(old); original.Applications.Add(CoreFixtures.App(current, false));
        var fault = Assert.Throws<ClientFault>(() => RecoveredApplicationPlan.Prepare(original, default));
        Assert.Equal("TRAFFIC_APPLICATION_MISSING", fault.Code);
        Assert.Equal(old, original.Applications[0].ExecutablePath); Assert.False(original.Applications[1].Enabled);
    }
    [Fact]
    public void FailureAfterRefreshAndCancellationNeverAlterTheOriginalIntent()
    {
        string old = Path.Combine(root, "Discord", "app-old", "Discord.exe");
        _ = Create("Discord", "app-new", "Discord.exe"); var original = Plan(old);
        Assert.Throws<ClientFault>(() => RecoveredApplicationPlan.Prepare(original, default,
            (_, _) => throw new ClientFault("TRAFFIC_APPLICATION_IDENTITY_MISMATCH")));
        using var cancellation = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => RecoveredApplicationPlan.Prepare(original, cancellation.Token,
            (candidate, _) => { cancellation.Cancel(); return candidate; }));
        Assert.Equal(old, Assert.Single(original.Applications).ExecutablePath);
        Assert.Throws<OperationCanceledException>(() => RecoveredApplicationPlan.Prepare(original, cancellation.Token,
            (_, _) => throw new InvalidOperationException("Cancelled preparation must not run.")));
    }
    [Fact]
    public void WholeComputerRecoveryNeverScansOrOpensOptionalApplicationPaths()
    {
        var original = Plan(Path.Combine(root, "Discord", "app-old", "Discord.exe")) with { Mode = RoutingMode.WholeComputer };
        Assert.Same(original, RecoveredApplicationPlan.Prepare(original, default,
            (_, _) => throw new InvalidOperationException("Whole-PC does not canonicalize app rules.")));
        var saved = new ProtectedTunnelIntent("fixture-owner", original);
        Assert.Same(saved, RestoredTunnelPlan.Refresh(saved));
    }
    [Fact]
    public void RefreshedRecoveryCannotBypassTheCompleteApplicationLimit()
    {
        string existing = Create("Discord", "app-old", "Discord.exe"); _ = Create("Discord", "app-new", "Discord.exe");
        var original = Plan(existing);
        original.Applications.AddRange(Enumerable.Range(0, 127).Select(index => CoreFixtures.App(Path.Combine(root, $"App{index}.exe"), false)));
        Assert.Equal("INVALID_PLAN", Assert.Throws<ClientFault>(() => RecoveredApplicationPlan.Prepare(original, default,
            (_, _) => throw new InvalidOperationException("Expanded invalid list cannot reach canonicalization."))).Code);
        Assert.Equal(128, original.Applications.Count);
    }
    private static ConnectPlan Plan(string path) => CoreFixtures.Plan() with { Applications = [CoreFixtures.App(path)] };
    private string Create(string family, string version, string executable)
    {
        string directory = Path.Combine(root, family, version); Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, executable); File.Copy(Environment.ProcessPath!, path); return path;
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
