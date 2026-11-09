using Slx.Client.Core;
using Slx.Client.Service;
using Slx.Client.Windows;
using Xunit;
namespace Slx.Client.Tests;

public sealed class Alpha29RecoveryTests
{
    [Theory]
    [InlineData(ProbeTarget.Gstatic)]
    [InlineData(ProbeTarget.Slx)]
    public async Task SuccessfulOriginDoesNotWaitForAnUnreachablePeer(ProbeTarget healthy)
    {
        bool peerCancelled = false;
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var passed = await StartupReadiness.CheckAsync(async (target, token) =>
        {
            if (target == healthy) return new(target, ProbeOutcome.Success, 1);
            try { await Task.Delay(Timeout.InfiniteTimeSpan, token); }
            catch (OperationCanceledException) { peerCancelled = true; throw; }
            throw new InvalidOperationException();
        }, budget.Token);
        Assert.True(passed); Assert.True(peerCancelled); Assert.False(budget.IsCancellationRequested);
    }
    [Fact]
    public void RecoveryMustRebuildAConfirmedStaleBindingButNotAnIsolatedCore()
    {
        var a = new OutboundBinding(Guid.NewGuid(), "Ethernet"); var b = new OutboundBinding(Guid.NewGuid(), "Wi-Fi");
        Assert.Equal(PhysicalPathState.Ready, PhysicalPathPolicy.Compare(a, a));
        Assert.Equal(PhysicalPathState.Unavailable, PhysicalPathPolicy.Compare(a, null));
        Assert.Equal(PhysicalPathState.Changed, PhysicalPathPolicy.Compare(a, b));
        var policy = new CoreRecoveryPolicy(); var healthy = new CoreRecoveryResources(true, true, true, true);
        Assert.Equal(TunnelLifecycleScope.VpnCore, policy.Choose(healthy, TunnelLifecycleReason.CoreFailure).Scope);
        policy.RequireFull(TunnelLifecycleReason.PhysicalInterfaceChanged);
        Assert.Equal(TunnelLifecycleReason.PhysicalInterfaceChanged, policy.Choose(healthy, TunnelLifecycleReason.CoreFailure).Reason);
        Assert.Equal(TunnelLifecycleScope.FullTunnel, policy.Choose(healthy, TunnelLifecycleReason.CoreFailure).Scope);
        policy.FullBaselineRestored(); Assert.Equal(TunnelLifecycleScope.VpnCore, policy.Choose(healthy, TunnelLifecycleReason.CoreFailure).Scope);
    }
    [Fact]
    public void PhysicalSelectionExcludesOurTunAndUsesPrefixThenEffectiveMetric()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        Assert.Equal(b, PhysicalEgress.Choose([
            new(OutboundInterface.TunnelIdentity, "renamed", 32, 0), new(a, "Ethernet", 0, 20), new(b, "Wi-Fi", 0, 5)])!.Identity);
        Assert.Equal(a, PhysicalEgress.Choose([new(a, "Ethernet", 24, 100), new(b, "Wi-Fi", 0, 5)])!.Identity);
        Assert.Null(PhysicalEgress.Choose([new(OutboundInterface.TunnelIdentity, "renamed", 32, 0)]));
    }
    [Fact]
    public void RotationCannotRewriteTheFailedExecutionOrItsJournalEvent()
    {
        var primary = CoreFixtures.Profile() with { Host = "1.1.1.1" };
        var alternate = CoreFixtures.Profile(CoreFixtures.Xhttp) with { Host = "1.1.1.1" };
        var rotation = new TransportRotation(); rotation.Reset(new(primary, RoutingMode.SelectedApplications, [CoreFixtures.App()], [alternate]));
        var execution = new TransportExecution(); var now = DateTimeOffset.UtcNow;
        execution.Starting(rotation, now); var started = execution.Current;
        var history = new TunnelLifecycleJournal();
        history.Record(TunnelLifecycleOrigin.AutomaticRecovery, new(TunnelLifecycleScope.VpnCore, TunnelLifecycleReason.CoreFailure),
            TunnelLifecycleOutcome.Failed, "CORE_EXITED", started);
        execution.Failed(); rotation.Advance("CORE_EXITED");
        var waiting = execution.Observe(rotation, now.AddSeconds(1));
        Assert.Null(waiting.Current); Assert.Equal("hysteria2", waiting.LastFailed!.Transport); Assert.Equal("vless-xhttp", waiting.Next!.Transport);
        execution.Starting(rotation, now.AddSeconds(2));
        Assert.Equal("vless-xhttp", execution.Current!.Transport);
        Assert.Equal(started, Assert.Single(history.Snapshot().Events).Transport);
    }
    [Theory]
    [InlineData("riot")]
    [InlineData("League of Legends")]
    [InlineData("RiotClientServices.exe")]
    [InlineData(@"D:\Games")]
    public void RiotShortcutSearchKeepsTheExactSharedExecutable(string query)
    {
        var rule = new AppRule(Guid.NewGuid(), "League of Legends", @"D:\Games\Riot Client\RiotClientServices.exe", false);
        var normalized = ApplicationGroups.Normalize(rule);
        Assert.Equal("Riot Client", ApplicationLabels.DisplayName(normalized)); Assert.Equal(rule.Name, normalized.Name);
        Assert.False(normalized.Enabled);
        Assert.Equal(rule.Id, normalized.Id); Assert.Equal(rule.ExecutablePath, normalized.ExecutablePath);
        Assert.True(ApplicationLabels.Matches(normalized, query));
        Assert.Equal("VALORANT", ApplicationLabels.DisplayName(rule with { Name = "VALORANT", ExecutablePath = @"D:\Games\VALORANT.exe" }));
    }
}
