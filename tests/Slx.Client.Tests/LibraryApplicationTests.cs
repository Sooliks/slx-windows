using Slx.Client.Windows;
using Xunit;
namespace Slx.Client.Tests;
public sealed class LibraryApplicationTests
{
    [Theory]
    [InlineData(@"C:\Program Files (x86)\Steam\uninstall.exe")]
    [InlineData(@"C:\Apps\Example\Uninstaller.exe")]
    [InlineData(@"C:\Apps\Example\unins000.exe")]
    [InlineData(@"C:\Apps\Example\uninst.exe")]
    [InlineData(@"C:\Apps\Example\UNWISE32.EXE")]
    [InlineData(@"C:\Apps\Example\uninstall-helper.exe")]
    [InlineData(@"C:\Apps\Example\deinstall.exe")]
    public void AutomaticLibraryExcludesUninstallExecutables(string path)=>Assert.False(LibraryApplication.IsCandidate(path));
    [Theory]
    [InlineData(@"C:\Program Files (x86)\Steam\steam.exe")]
    [InlineData(@"C:\Apps\Browser\browser.exe")]
    [InlineData(@"C:\Apps\Telegram\Telegram.exe")]
    [InlineData(@"C:\Apps\uninstall-folder\App.exe")]
    [InlineData(@"C:\Apps\Tools\BulkCrapUninstaller.exe")]
    public void ProgramAndParentDirectoryNamesArePreserved(string path)=>Assert.True(LibraryApplication.IsCandidate(path));
}
