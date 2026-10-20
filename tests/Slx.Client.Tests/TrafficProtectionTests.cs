using Slx.Client.Core;
using Slx.Client.Windows;
using Xunit;
namespace Slx.Client.Tests;
/// <summary>Pure preparation/error contracts and read-only app-ID calls; never changes WFP or adapters.</summary>
public sealed class TrafficProtectionTests
{
    [Fact]
    public void MissingVersionCannotBeSilentlyDroppedFromSelectedProtection()
    {
        var plan=CoreFixtures.Plan();
        var app=plan.Applications[0];
        plan=plan with { Applications=[app,app with{Id=Guid.NewGuid(),ExecutablePath=@"C:\Apps\missing-version\RobloxPlayerBeta.exe"}] };
        int calls=0;
        var error=Assert.Throws<ClientFault>(()=>TrafficApplicationIds.Resolve(plan,"core","service",(path,selected)=>
        {
            Assert.True(selected);calls++;
            if(calls==2)TrafficProtectionError.Application(3,selected);
            return [1,2];
        }));
        Assert.Equal("TRAFFIC_APPLICATION_MISSING",error.Code);
        Assert.Equal(2,calls);
    }
    [Fact]
    public void DisabledApplicationsAreNotRequiredAndWholeComputerKeepsOnlyOwnedExemptions()
    {
        var plan=CoreFixtures.Plan();var enabled=plan.Applications[0];
        plan=plan with{Applications=[enabled,enabled with{Id=Guid.NewGuid(),Enabled=false,ExecutablePath=@"C:\Removed\app.exe"}]};
        var selected=TrafficApplicationIds.Resolve(plan,"core","service",(path,isSelected)=>
        {Assert.Equal(enabled.ExecutablePath,path);Assert.True(isSelected);return [1];});
        Assert.False(Assert.Single(selected).Permit);
        var paths=new List<string>();
        var all=TrafficApplicationIds.Resolve(plan with{Mode=RoutingMode.WholeComputer},"core","service",(path,isSelected)=>
        {Assert.False(isSelected);paths.Add(path);return [2];});
        Assert.Equal(new[]{"core","service"},paths);
        Assert.All(all,app=>Assert.True(app.Permit));
    }
    [Theory]
    [InlineData(2u,"TRAFFIC_APPLICATION_MISSING")]
    [InlineData(3u,"TRAFFIC_APPLICATION_MISSING")]
    [InlineData(5u,"TRAFFIC_APPLICATION_DENIED")]
    [InlineData(0x80320012u,"TRAFFIC_PROTECTION_APPLICATION_80320012")]
    public void NativeAppFailuresHaveBoundedActionableCodes(uint code,string expected)
        =>Assert.Equal(expected,Assert.Throws<ClientFault>(()=>TrafficProtectionError.Application(code,true)).Code);
    [Fact]
    public void MissingOwnedExemptionIsNeverMisclassifiedAsAUserApplication()
        =>Assert.Equal("TRAFFIC_PROTECTION_APPLICATION_00000002",
            Assert.Throws<ClientFault>(()=>TrafficProtectionError.Application(2,false)).Code);
    [Fact]
    public void NativeAppIdentityDetectsRemovedVersionWithoutOpeningOrChangingTheFilterEngine()
    {
        if(!OperatingSystem.IsWindows())return;
        string root=Path.Combine(Path.GetTempPath(),"slx-app-id-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string executable=Path.Combine(root,"RobloxPlayerBeta.exe");File.WriteAllBytes(executable,[0]);
            var identity=TrafficApplicationIds.Read(executable,true);Assert.NotEmpty(identity);
            File.Delete(executable);
            Assert.Equal("TRAFFIC_APPLICATION_MISSING",Assert.Throws<ClientFault>(()=>TrafficApplicationIds.Read(executable,true)).Code);
            Directory.Delete(root);
            Assert.Equal("TRAFFIC_APPLICATION_MISSING",Assert.Throws<ClientFault>(()=>TrafficApplicationIds.Read(executable,true)).Code);
        }
        finally{if(Directory.Exists(root))Directory.Delete(root);}
    }
    [Fact]
    public void DiagnosticCodesContainOnlyOperationAndNumericWindowsFailure()
    {
        var fault=Assert.Throws<ClientFault>(()=>TrafficProtectionError.Check(0x800706BA,TrafficOperation.Open));
        Assert.Equal("TRAFFIC_PROTECTION_OPEN_800706BA",fault.Code);
        Assert.DoesNotContain(@"C:\",fault.Message);
        Assert.Equal("TrafficApplicationId [redacted]",new TrafficApplicationId([65,66],false).ToString());
    }
}
