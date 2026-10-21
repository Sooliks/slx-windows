using Slx.Client.Core;
using Slx.Client.Windows;
using Xunit;
namespace Slx.Client.Tests;
/// <summary>Real app-only transaction planner and injected commit failures; never opens WFP.</summary>
public sealed class TrafficApplicationPolicyTests
{
    [Fact]
    public void OpaqueSnapshotsCopyNativeBytesAndUnionUsesExactIdentity()
    {
        byte[] original=[1,2,3];
        var before=TrafficApplicationPolicy.FromIdentities([original]);original[0]=99;
        var target=TrafficApplicationPolicy.FromIdentities([[1,2,3],[4,5,6]]);
        Assert.Equal(2,before.Union(target).Count);
        Assert.Equal(new byte[]{1,2,3},Assert.Single(before.Entries).Value);
        Assert.Equal("TrafficApplicationPolicy [redacted]",before.ToString());
    }
    [Fact]
    public void EqualIdentitySetDoesNotOpenANativeTransactionOrRewriteFilterKeys()
    {
        var before=TrafficApplicationPolicy.FromIdentities([[1],[2]]);var state=new TrafficApplicationState(before);
        var filters=state.Filters.ToDictionary(entry=>entry.Key,entry=>entry.Value);
        state.Apply(TrafficApplicationPolicy.FromIdentities([[2],[1],[2]]),_=>throw new InvalidOperationException("No WFP change expected."));
        Assert.Same(before,state.Current);
        foreach(var entry in filters)Assert.Same(entry.Value,state.Filters[entry.Key]);
    }
    [Fact]
    public void UnionProtectsAddedApplicationsThenFinalPolicyRemovesOnlyDepartedPairs()
    {
        var before=TrafficApplicationPolicy.FromIdentities([[1],[2]]);
        var desired=TrafficApplicationPolicy.FromIdentities([[2],[3]]);var state=new TrafficApplicationState(before);
        var retained=state.Filters["02"];var departed=state.Filters["01"];
        state.Apply(before.Union(desired),update=>
        {
            Assert.Single(update.Added);Assert.Empty(update.Removed);
            Assert.Equal(new byte[]{3},update.Added[0].Identity);
        });
        var added=state.Filters["03"];
        Assert.Same(retained,state.Filters["02"]);Assert.Same(departed,state.Filters["01"]);
        state.Apply(desired,update=>
        {
            Assert.Empty(update.Added);Assert.Same(departed,Assert.Single(update.Removed));
        });
        Assert.Same(desired,state.Current);Assert.Same(retained,state.Filters["02"]);Assert.Same(added,state.Filters["03"]);
    }
    [Fact]
    public void RollbackUsesCapturedNativeIdentityWithoutReopeningAnExecutable()
    {
        var before=TrafficApplicationPolicy.FromIdentities([[1]]);var desired=TrafficApplicationPolicy.FromIdentities([[2]]);
        var state=new TrafficApplicationState(before);var original=state.Filters["01"];
        state.Apply(before.Union(desired),_=>{});
        var newFilter=state.Filters["02"];
        state.Apply(before,update=>
        {
            Assert.Empty(update.Added);Assert.Same(newFilter,Assert.Single(update.Removed));
        });
        Assert.Same(original,Assert.Single(state.Filters).Value);Assert.Same(before,state.Current);
    }
    [Fact]
    public void FailedNativeCommitNeverAdvancesTheCommittedMapAndDisablesUnsafeFurtherDeltas()
    {
        var before=TrafficApplicationPolicy.FromIdentities([[1]]);var state=new TrafficApplicationState(before);
        var committed=Assert.Single(state.Filters).Value;
        var desired=TrafficApplicationPolicy.FromIdentities([[2]]);
        Assert.Throws<IOException>(()=>state.Apply(desired,_=>throw new IOException("Injected commit failure")));
        Assert.Same(committed,Assert.Single(state.Filters).Value);
        Assert.Equal("TRAFFIC_PROTECTION_APPLICATION_BASELINE",Assert.Throws<ClientFault>(()=>state.Current).Code);
        Assert.Throws<ClientFault>(()=>state.Apply(before,_=>throw new InvalidOperationException("Invalidated baseline cannot reach WFP.")));
        Assert.Equal(1,before.Count);
    }
    [Fact]
    public void NativeSessionInvalidationPreventsAnEqualSetFromPretendingProtectionIsVerified()
    {
        var before=TrafficApplicationPolicy.FromIdentities([[1]]);var state=new TrafficApplicationState(before);state.Invalidate();
        Assert.Throws<ClientFault>(()=>state.Apply(before,_=>throw new InvalidOperationException("No native call allowed.")));
    }
    [Fact]
    public void UnionOfTwoMaximumPlansIsBoundedAndCannotGrowWithoutLimit()
    {
        static byte[] Identity(int value)=>BitConverter.GetBytes(value);
        var first=TrafficApplicationPolicy.FromIdentities(Enumerable.Range(0,128).Select(Identity));
        var second=TrafficApplicationPolicy.FromIdentities(Enumerable.Range(128,128).Select(Identity));
        var union=first.Union(second);Assert.Equal(256,union.Count);
        Assert.Equal("TRAFFIC_PROTECTION_APPLICATION_LIMIT",Assert.Throws<ClientFault>(()=>union.Union(
            TrafficApplicationPolicy.FromIdentities([Identity(256)]))).Code);
    }
    [Fact]
    public void NewApplicationOwnsDistinctNonemptyIpv4AndIpv6FilterKeys()
    {
        var state=new TrafficApplicationState(TrafficApplicationPolicy.FromIdentities([[1],[2]]));
        var keys=state.Filters.Values.SelectMany(filter=>new[]{filter.V4,filter.V6}).ToArray();
        Assert.DoesNotContain(Guid.Empty,keys);Assert.Equal(4,keys.Distinct().Count());
        Assert.All(state.Filters.Values,filter=>Assert.Equal("TrafficApplicationFilter [redacted]",filter.ToString()));
    }
}
