using Slx.Client.Windows;
using Xunit;
namespace Slx.Client.Tests;
public sealed class DisplayIconPathTests
{
    [Theory]
    [InlineData(@"C:\Apps\Vendor, Inc\app.exe", @"C:\Apps\Vendor, Inc\app.exe")]
    [InlineData("\"C:\\Apps\\Vendor, Inc\\app.exe\"", @"C:\Apps\Vendor, Inc\app.exe")]
    [InlineData("\"C:\\Apps\\Vendor, Inc\\app.exe\",0", @"C:\Apps\Vendor, Inc\app.exe")]
    [InlineData(@" C:\Apps\Vendor, Inc\app.exe, -123 ", @"C:\Apps\Vendor, Inc\app.exe")]
    [InlineData(@"C:\Apps\app.exe,+5", @"C:\Apps\app.exe")]
    [InlineData("\"C:\\Apps\\app,123.exe\"", @"C:\Apps\app,123.exe")]
    [InlineData(@"C:\Apps\app.exe,resource", @"C:\Apps\app.exe,resource")]
    [InlineData(@"C:\Apps\app.exe,", @"C:\Apps\app.exe,")]
    [InlineData(@"C:\Apps\app.exe,2147483648", @"C:\Apps\app.exe,2147483648")]
    public void OnlyNumericIndexOutsideQuotesIsRemoved(string location, string path) =>
        Assert.Equal(path, DisplayIconPath.Parse(location));
    [Fact]
    public void EnvironmentExpansionPreservesACommaInsideTheDirectory()
    {
        var expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Vendor, Inc", "app.exe");
        Assert.Equal(expected, DisplayIconPath.Parse("\"%LOCALAPPDATA%\\Vendor, Inc\\app.exe\",-1"));
    }
    [Fact]
    public void RealCommaPathRemainsDiscoverableWithoutExecutingTheFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), "slx-icon-location-" + Guid.NewGuid().ToString("N"), "Vendor, Inc");
        Directory.CreateDirectory(directory);
        var executable = Path.Combine(directory, "app.exe");
        try
        {
            File.WriteAllBytes(executable, [0]);
            Assert.Equal(executable, AppDiscovery.FromExecutable(DisplayIconPath.Parse(executable)).ExecutablePath);
            if (OperatingSystem.IsWindows()) Assert.NotEmpty(TrafficApplicationIds.Read(DisplayIconPath.Parse(executable), true));
        }
        finally { File.Delete(executable); Directory.Delete(directory); Directory.Delete(Path.GetDirectoryName(directory)!); }
    }
}
