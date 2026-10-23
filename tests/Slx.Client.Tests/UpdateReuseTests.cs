using Slx.Client.Windows;
using Xunit;

namespace Slx.Client.Tests;

public sealed class UpdateReuseTests
{
    // Owner request: an installer of the needed version that is already staged is not downloaded again.
    [Fact]
    public async Task VerifiedStagedInstallerIsReusedInsteadOfDownloadedAgain()
    {
        using var fixture = new UpdateFixtures();
        using var handler = new UpdateFixtures.Handler((_, _) => Task.FromResult(UpdateFixtures.Response(UpdateFixtures.Installer)));
        var client = fixture.Client(handler, _ => true);
        var first = await client.DownloadAsync(UpdateFixtures.Offer());
        Assert.Equal(1, handler.Calls);
        var progress = new UpdateFixtures.Progress();
        var again = await client.DownloadAsync(UpdateFixtures.Offer(), progress);
        Assert.Equal(1, handler.Calls); Assert.Equal(first.InstallerPath, again.InstallerPath); Assert.Equal(new[] { 100 }, progress.Values);
        Assert.Equal(first.InstallerPath, (await client.FindPreparedAsync(UpdateFixtures.Offer()))!.InstallerPath);
        File.Delete(first.InstallerPath + ":Zone.Identifier");
        var restored = await client.FindPreparedAsync(UpdateFixtures.Offer());
        Assert.Contains("ZoneId=3", await File.ReadAllTextAsync(restored!.InstallerPath + ":Zone.Identifier"));
        Assert.Equal(UpdateLaunchResult.Started, await client.LaunchInstallerAsync(restored));
        Assert.Null(await client.FindPreparedAsync(UpdateFixtures.Offer("0.1.0-alpha.12")));
        Assert.Equal(1, handler.Calls);
    }
    [Fact]
    public async Task DamagedOrForeignStagedFilesAreNeverReused()
    {
        using var fixture = new UpdateFixtures();
        using var handler = new UpdateFixtures.Handler((_, _) => Task.FromResult(UpdateFixtures.Response(UpdateFixtures.Installer)));
        var client = fixture.Client(handler);
        var first = await client.DownloadAsync(UpdateFixtures.Offer());
        await File.WriteAllBytesAsync(first.InstallerPath, new byte[UpdateFixtures.Installer.Length]);
        var foreign = Path.Combine(fixture.Root, "not-a-staging-folder");
        Directory.CreateDirectory(foreign);
        await File.WriteAllBytesAsync(Path.Combine(foreign, UpdateFixtures.Offer().AssetName), UpdateFixtures.Installer);
        Assert.Null(await client.FindPreparedAsync(UpdateFixtures.Offer()));
        var fresh = await client.DownloadAsync(UpdateFixtures.Offer());
        Assert.Equal(2, handler.Calls); Assert.NotEqual(first.InstallerPath, fresh.InstallerPath);
        Assert.Equal(UpdateFixtures.Installer, await File.ReadAllBytesAsync(fresh.InstallerPath));
    }
}
