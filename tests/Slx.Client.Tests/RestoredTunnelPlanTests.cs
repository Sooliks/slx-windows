using Slx.Client.Core;
using Slx.Client.Service;
using Slx.Client.Windows;
using Xunit;
namespace Slx.Client.Tests;
/// <summary>Real resume-plan preparation against local files; no service, WFP, DPAPI or network changes.</summary>
public sealed class RestoredTunnelPlanTests : IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"slx-resume-"+Guid.NewGuid().ToString("N"));
    [Theory]
    [InlineData("Discord","app-old","app-new","Discord.exe")]
    [InlineData(@"Roblox\Versions","version-old","version-new","RobloxPlayerBeta.exe")]
    public void RebootPreparationRepairsRemovedVersionAndPreservesPinnedEndpointAndOwner(string application,string oldVersion,string newVersion,string executable)
    {
        var missing=Path.Combine(root,application,oldVersion,executable);
        var current=Create(application,newVersion,executable);
        var rule=CoreFixtures.App(missing);
        var original=new ProtectedTunnelIntent("S-1-5-21-1-2-3-1001",
            CoreFixtures.Plan() with{Profile=CoreFixtures.Profile() with{Host="1.1.1.1"},Applications=[rule]},"vpn.example.com");
        var restored=RestoredTunnelPlan.Refresh(original);
        Assert.NotSame(original,restored);
        Assert.Equal(current,Assert.Single(restored.Plan.Applications).ExecutablePath);
        Assert.Equal(missing,Assert.Single(original.Plan.Applications).ExecutablePath);
        Assert.Equal(rule.Id,restored.Plan.Applications[0].Id);
        Assert.Equal(original.OwnerSid,restored.OwnerSid);Assert.Equal(original.EndpointHost,restored.EndpointHost);
        Assert.Same(original.Plan.Profile,restored.Plan.Profile);Assert.Equal(original.Plan.Mode,restored.Plan.Mode);
        PlanValidator.Validate(restored.Plan);
    }
    [Fact]
    public void ExistingVersionAndNewSiblingAreBothPresentInThePlanUsedForProtectionAndLaunch()
    {
        var previous=Create("Discord","app-old","Discord.exe");var current=Create("Discord","app-new","Discord.exe");
        var rule=CoreFixtures.App(previous);
        var saved=new ProtectedTunnelIntent("owner",CoreFixtures.Plan() with{Applications=[rule]});
        var restored=RestoredTunnelPlan.Refresh(saved);
        Assert.Equal(2,restored.Plan.Applications.Count);
        Assert.Contains(rule,restored.Plan.Applications);
        Assert.Contains(restored.Plan.Applications,app=>app.ExecutablePath==current&&app.Enabled);
        Assert.Equal(2,restored.Plan.Applications.Select(app=>app.Id).Distinct().Count());
    }
    [Fact]
    public void UnresolvedPathsAndTheirEnabledStateAreNeverDroppedDuringResumePreparation()
    {
        var missing=CoreFixtures.App(Path.Combine(root,"Roblox","Versions","version-missing","RobloxPlayerBeta.exe"));
        var unrelated=CoreFixtures.App(Path.Combine(root,"MissingHelper.exe"),false);
        var saved=new ProtectedTunnelIntent("owner",CoreFixtures.Plan() with{Applications=[missing,unrelated]});
        var restored=RestoredTunnelPlan.Refresh(saved);
        Assert.Same(saved,restored);
        Assert.Equal(new[]{missing,unrelated},restored.Plan.Applications);
    }
    [Fact]
    public void ExpandedResumePlanCannotBypassThePrivilegedApplicationLimit()
    {
        var previous=Create("Discord","app-old","Discord.exe");_ = Create("Discord","app-new","Discord.exe");
        var rules=Enumerable.Range(0,127).Select(index=>CoreFixtures.App(Path.Combine(root,$"App{index}.exe"))).ToList();
        rules.Add(CoreFixtures.App(previous));
        var saved=new ProtectedTunnelIntent("owner",CoreFixtures.Plan() with{Applications=rules});
        PlanValidator.Validate(saved.Plan);
        Assert.Equal("INVALID_PLAN",Assert.Throws<ClientFault>(()=>RestoredTunnelPlan.Refresh(saved)).Code);
        Assert.Equal(128,saved.Plan.Applications.Count);
    }
    private string Create(string application,string version,string executable)
    {
        var directory=Path.Combine(root,application,version);Directory.CreateDirectory(directory);
        var path=Path.Combine(directory,executable);File.WriteAllText(path,"resume path fixture, never executed");return path;
    }
    public void Dispose(){if(Directory.Exists(root))Directory.Delete(root,true);}
}
