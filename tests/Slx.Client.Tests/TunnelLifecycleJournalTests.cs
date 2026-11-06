using System.Reflection;
using System.Text.Json;
using Slx.Client.Core;
using Slx.Client.Service;
using Xunit;

namespace Slx.Client.Tests;

public sealed class TunnelLifecycleJournalTests
{
    [Fact]
    public void RestoreOriginIsConsumedBeforeWorkSoALaterFailureCannotInheritIt()
    {
        var engine = new TunnelEngine();
        Field("recoveryOrigin").SetValue(engine, TunnelLifecycleOrigin.ServiceRestore);
        Assert.Equal(TunnelLifecycleOrigin.ServiceRestore, engine.TakeRecoveryOrigin());
        // The first operation can fail or be cancelled; no success callback is needed to consume its origin.
        Assert.Equal(TunnelLifecycleOrigin.AutomaticRecovery, engine.TakeRecoveryOrigin());
    }

    [Fact]
    public void CoreReplacementPreservesTunGenerationAndRecordsItsActualScope()
    {
        var journal = new TunnelLifecycleJournal();
        journal.TunStarted(); journal.CoreStarted();
        journal.Record(TunnelLifecycleOrigin.ConnectRequest, new(TunnelLifecycleScope.FullTunnel, TunnelLifecycleReason.Requested),
            TunnelLifecycleOutcome.Connected);
        var first = journal.Snapshot();
        journal.Record(TunnelLifecycleOrigin.AutomaticRecovery, new(TunnelLifecycleScope.VpnCore, TunnelLifecycleReason.HealthProbeFailed),
            TunnelLifecycleOutcome.Started);
        journal.CoreStarted();
        journal.Record(TunnelLifecycleOrigin.AutomaticRecovery, new(TunnelLifecycleScope.VpnCore, TunnelLifecycleReason.HealthProbeFailed),
            TunnelLifecycleOutcome.Connected);
        var after = journal.Snapshot();
        Assert.Equal(first.Since, after.Since);
        Assert.Equal((1, 2), (after.TunGeneration, after.CoreGeneration));
        Assert.Equal(1, Assert.Single(first.Events).CoreGeneration);
        Assert.Equal((TunnelLifecycleScope.VpnCore, 1, 2),
            (after.Events[^1].Scope, after.Events[^1].TunGeneration, after.Events[^1].CoreGeneration));
    }

    [Fact]
    public void HistoryIsBoundedOrderedAndDoesNotExposeUnknownErrorText()
    {
        var journal = new TunnelLifecycleJournal();
        for (int index = 0; index < 40; index++)
            journal.Record(TunnelLifecycleOrigin.AutomaticRecovery,
                new(TunnelLifecycleScope.VpnCore, TunnelLifecycleReason.CoreFailure), TunnelLifecycleOutcome.Failed,
                index % 2 == 0 ? "CORE_START_FAILED" : "secret-profile.example password=credential");
        var snapshot = journal.Snapshot();
        Assert.Equal(TunnelLifecycleObservation.MaximumEvents, snapshot.Events.Count);
        Assert.Equal(Enumerable.Range(25, 16).Select(value => (long)value), snapshot.Events.Select(item => item.Sequence));
        Assert.Contains(snapshot.Events, item => item.Code == "CORE_START_FAILED");
        Assert.Contains(snapshot.Events, item => item.Code is null);
        string json = JsonSerializer.Serialize(snapshot);
        Assert.DoesNotContain("secret-profile", json); Assert.DoesNotContain("credential", json);
    }

    [Fact]
    public void ProductionStatusOmitsRetainedHistoryWhileOffForStrictOlderUpdaters()
    {
        var engine = new TunnelEngine();
        var journal = (TunnelLifecycleJournal)Field("lifecycleJournal").GetValue(engine)!;
        journal.TunStarted(); journal.CoreStarted();
        journal.Record(TunnelLifecycleOrigin.AutomaticRecovery, new(TunnelLifecycleScope.FullTunnel, TunnelLifecycleReason.FrontUnavailable),
            TunnelLifecycleOutcome.Failed, "CORE_EXITED");
        Field("desiredConnected").SetValue(engine, true);
        Assert.NotNull(engine.Status.Lifecycle);
        Field("desiredConnected").SetValue(engine, false);
        Assert.Null(engine.Status.Lifecycle);
        Assert.DoesNotContain("Lifecycle", JsonSerializer.Serialize(engine.Status));
        Field("desiredConnected").SetValue(engine, true);
        Assert.Single(engine.Status.Lifecycle!.Events);
    }

    private static FieldInfo Field(string name) => typeof(TunnelEngine).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
}
