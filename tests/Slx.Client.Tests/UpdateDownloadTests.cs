using System.ComponentModel;
using System.Diagnostics;
using System.Net.Http;
using System.Security.AccessControl;
using System.Security.Principal;
using Slx.Client.Core;
using Slx.Client.Windows;
using Xunit;

namespace Slx.Client.Tests;

public sealed class UpdateDownloadTests
{
    [Fact]
    public async Task VerifiedDownloadStaysIdleUntilExplicitInteractiveInstallerLaunch()
    {
        using var fixture = new UpdateFixtures(); ProcessStartInfo? launched = null;
        using var handler = new UpdateFixtures.Handler((_, _) => Task.FromResult(UpdateFixtures.Response(UpdateFixtures.Installer)));
        var client = fixture.Client(handler, info => { launched = info; return true; });
        var progress = new UpdateFixtures.Progress();
        var prepared = await client.DownloadAsync(UpdateFixtures.Offer(), progress);
        Assert.Null(launched); Assert.Equal(UpdateFixtures.Installer, await File.ReadAllBytesAsync(prepared.InstallerPath));
        Assert.Contains("ZoneId=3", await File.ReadAllTextAsync(prepared.InstallerPath + ":Zone.Identifier"));
        Assert.Equal(0, progress.Values.First()); Assert.Equal(100, progress.Values.Last());
        var acl = new DirectoryInfo(Path.GetDirectoryName(prepared.InstallerPath)!).GetAccessControl();
        Assert.True(acl.AreAccessRulesProtected);
        using var identity = WindowsIdentity.GetCurrent();
        var allowed = new[] { identity.User!.Value, "S-1-5-18", "S-1-5-32-544" };
        Assert.All(acl.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>(), rule => Assert.Contains(rule.IdentityReference.Value, allowed));
        Assert.Equal(UpdateLaunchResult.Started, await client.LaunchInstallerAsync(prepared));
        Assert.Equal(prepared.InstallerPath, launched!.FileName); Assert.Equal("runas", launched.Verb);
        Assert.True(launched.UseShellExecute); Assert.Empty(launched.Arguments); Assert.Empty(launched.ArgumentList);
    }
    [Theory]
    [InlineData("hash")]
    [InlineData("short")]
    [InlineData("oversize-stream")]
    [InlineData("timeout")]
    public async Task FailedDownloadLeavesNoPartOrRunnableInstaller(string kind)
    {
        using var fixture = new UpdateFixtures();
        using var handler = new UpdateFixtures.Handler((_, _) => Task.FromResult(kind switch
        {
            "short" => UpdateFixtures.Response(UpdateFixtures.Installer[..^1]),
            "oversize-stream" => new(System.Net.HttpStatusCode.OK) { Content = new StreamContent(new UpdateFixtures.UnknownLengthStream(new byte[UpdateFixtures.Installer.Length + 1])) },
            "timeout" => new(System.Net.HttpStatusCode.OK) { Content = new StreamContent(new UpdateFixtures.StalledStream()) },
            _ => UpdateFixtures.Response(UpdateFixtures.Installer)
        }));
        var offer = kind == "hash" ? UpdateFixtures.Offer() with { Sha256 = new string('0', 64) } : UpdateFixtures.Offer();
        await Assert.ThrowsAsync<ClientFault>(() => fixture.Client(handler, timeout: TimeSpan.FromMilliseconds(100)).DownloadAsync(offer));
        Assert.Empty(Directory.EnumerateFiles(fixture.Root, "*", SearchOption.AllDirectories));
    }
    [Fact]
    public async Task InstallerIsRehashedBeforeLaunchAndTamperingNeverLaunches()
    {
        using var fixture = new UpdateFixtures();
        using var handler = new UpdateFixtures.Handler((_, _) => Task.FromResult(UpdateFixtures.Response(UpdateFixtures.Installer)));
        var client = fixture.Client(handler);
        var prepared = await client.DownloadAsync(UpdateFixtures.Offer());
        await File.WriteAllBytesAsync(prepared.InstallerPath, new byte[UpdateFixtures.Installer.Length]);
        var error = await Assert.ThrowsAsync<ClientFault>(() => client.LaunchInstallerAsync(prepared));
        Assert.Equal("UPDATE_INTEGRITY_FAILED", error.Code);
    }
    [Fact]
    public async Task FileCannotBeReplacedBetweenFinalVerificationAndLaunch()
    {
        using var fixture = new UpdateFixtures();
        using var handler = new UpdateFixtures.Handler((_, _) => Task.FromResult(UpdateFixtures.Response(UpdateFixtures.Installer)));
        var client = fixture.Client(handler, info =>
        {
            Assert.Throws<IOException>(() => File.WriteAllBytes(info.FileName, [0]));
            Assert.Throws<IOException>(() => File.Move(info.FileName, info.FileName + ".replaced"));
            return true;
        });
        var prepared = await client.DownloadAsync(UpdateFixtures.Offer());
        Assert.Equal(UpdateLaunchResult.Started, await client.LaunchInstallerAsync(prepared));
    }
    [Fact]
    public async Task PreparedPathCannotLaunchAnotherLocalOrNetworkExecutable()
    {
        using var fixture = new UpdateFixtures();
        using var handler = new UpdateFixtures.Handler((_, _) => throw new InvalidOperationException("No download expected"));
        var client = fixture.Client(handler);
        foreach (var path in new[] { @"C:\Windows\System32\cmd.exe", @"\\host\share\setup.exe", Path.Combine(fixture.Root, "..", "setup.exe") })
            Assert.Equal("UPDATE_INVALID", (await Assert.ThrowsAsync<ClientFault>(() => client.LaunchInstallerAsync(new(UpdateFixtures.Offer(), path)))).Code);
    }
    [Fact]
    public async Task UacCancellationIsObservedWithoutRetry()
    {
        using var fixture = new UpdateFixtures(); var launches = 0;
        using var handler = new UpdateFixtures.Handler((_, _) => Task.FromResult(UpdateFixtures.Response(UpdateFixtures.Installer)));
        var client = fixture.Client(handler, _ => { launches++; throw new Win32Exception(1223); });
        var prepared = await client.DownloadAsync(UpdateFixtures.Offer());
        Assert.Equal(UpdateLaunchResult.Cancelled, await client.LaunchInstallerAsync(prepared)); Assert.Equal(1, launches);
    }
}
