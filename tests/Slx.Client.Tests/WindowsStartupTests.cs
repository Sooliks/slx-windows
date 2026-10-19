using Microsoft.Win32;
using Slx.Client.Core;
using Slx.Client.Windows;
using Xunit;
namespace Slx.Client.Tests;
public sealed class WindowsStartupTests : IDisposable
{
    private readonly string path = @"Software\SLX\Tests\Startup\" + Guid.NewGuid().ToString("N");
    private const string Executable = @"C:\Program Files\SLX VPN\SLX VPN.exe";
    [Fact]
    public void SavedOptInRepairsMissingOrObsoleteRegistrationWithoutTouchingOtherApps()
    {
        using var key=Registry.CurrentUser.CreateSubKey(path,true);
        key.SetValue("Another app","preserved");
        StartupRegistration.Apply(key,true,Executable);
        Assert.Equal("\""+Executable+"\" --tray",key.GetValue("SLX VPN"));
        key.SetValue("SLX VPN",@"C:\Old\SLX VPN.exe");
        StartupRegistration.Apply(key,true,Executable);
        Assert.Equal("\""+Executable+"\" --tray",key.GetValue("SLX VPN"));
        Assert.Equal("preserved",key.GetValue("Another app"));
        StartupRegistration.Apply(key,true,Executable);
        Assert.Equal(2,key.ValueCount);
    }
    [Fact]
    public void OptOutRemovesOnlyOwnValueAndDoesNotRequireAnExecutable()
    {
        using var key=Registry.CurrentUser.CreateSubKey(path,true);
        key.SetValue("Another app","preserved");key.SetValue("SLX VPN","old");
        StartupRegistration.Apply(key,false,null);
        StartupRegistration.Apply(key,false,null);
        Assert.Null(key.GetValue("SLX VPN"));Assert.Equal("preserved",key.GetValue("Another app"));
    }
    [Theory]
    [InlineData("relative.exe")]
    [InlineData(@"C:\Runtime\dotnet.exe")]
    [InlineData("C:\\Bad\"\\SLX VPN.exe")]
    public void InvalidHostOrCommandInjectionCannotBeRegistered(string executable)
    {
        using var key=Registry.CurrentUser.CreateSubKey(path,true);
        Assert.Equal("STARTUP_UNAVAILABLE",Assert.Throws<ClientFault>(()=>StartupRegistration.Apply(key,true,executable)).Code);
        Assert.Null(key.GetValue("SLX VPN"));
    }
    public void Dispose()=>Registry.CurrentUser.DeleteSubKeyTree(path,false);
}
