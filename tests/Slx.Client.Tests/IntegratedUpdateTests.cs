using System.ComponentModel;
using Slx.Client.Core;
using Slx.Client.Windows;
using Xunit;

namespace Slx.Client.Tests;

public sealed class IntegratedUpdateTests
{
    [Theory]
    [InlineData("../SLX VPN Updater.exe")]
    [InlineData("C:/SLX VPN Updater.exe")]
    [InlineData("x/../../SLX VPN Updater.exe")]
    [InlineData("x//SLX VPN Updater.exe")]
    [InlineData("a/b/c/SLX VPN Updater.exe")]
    [InlineData("SLX VPN Updater.exe:stream")]
    public void BundleRejectsEscapesBeforeAnyFileCopy(string name) => Assert.Throws<ClientFault>(() =>
        IntegratedUpdateStaging.Validate(new([new(name, 1, UpdateFixtures.Digest)])));
    [Fact]
    public void BundleAllowsBoundedCultureFilesButRequiresExactHostAndUniqueNames()
    {
        var host = new IntegratedUpdateBundleFile("SLX VPN Updater.exe", 5, UpdateFixtures.Digest);
        IntegratedUpdateStaging.Validate(new([host, new("ru/PresentationFramework.resources.dll", 8, UpdateFixtures.Digest)]));
        Assert.Throws<ClientFault>(() => IntegratedUpdateStaging.Validate(new([host, host with { Name = "slx vpn updater.exe" }])));
        Assert.Throws<ClientFault>(() => IntegratedUpdateStaging.Validate(new([host with { Name = "other.exe" }])));
        Assert.Throws<ClientFault>(() => IntegratedUpdateStaging.Validate(new([host with { Size = 0 }])));
        Assert.Throws<ClientFault>(() => IntegratedUpdateStaging.Validate(new([host with { Sha256 = "bad" }])));
        Assert.Throws<ClientFault>(() => IntegratedUpdateStaging.Validate(new([host with { Size = long.MaxValue }, host with { Name = "other.dll", Size = long.MaxValue }])));
    }
    [Theory]
    [InlineData(0)]
    [InlineData(3010)]
    [InlineData(5)]
    public async Task SilentInstallerKeepsVerifiedFileLockedUntilObservedExitAndUsesOnlyFixedArguments(int exitCode)
    {
        using var fixture = new UpdateFixtures();
        using var handler = new UpdateFixtures.Handler((_, _) => Task.FromResult(UpdateFixtures.Response(UpdateFixtures.Installer)));
        var prepared = await fixture.Client(handler).DownloadAsync(UpdateFixtures.Offer()); var id = Guid.NewGuid();
        var result = await IntegratedUpdateInstaller.ExecuteAsync(prepared.InstallerPath, prepared.Offer, id, async info =>
        {
            Assert.Equal(prepared.InstallerPath, info.FileName); Assert.True(info.UseShellExecute); Assert.Equal("runas", info.Verb);
            Assert.Equal("/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /RESTARTEXITCODE=3010 /SLXUPDATE=" + id.ToString("N"), info.Arguments);
            Assert.Empty(info.ArgumentList); Assert.Throws<IOException>(() => File.WriteAllBytes(info.FileName, [0]));
            await Task.Yield(); Assert.Throws<IOException>(() => File.Move(info.FileName, info.FileName + ".changed")); return exitCode;
        });
        Assert.Equal(exitCode, result);
    }
    [Fact]
    public async Task CancelledPermissionDoesNotRetryAndTamperedInstallerNeverLaunches()
    {
        using var fixture = new UpdateFixtures();
        using var handler = new UpdateFixtures.Handler((_, _) => Task.FromResult(UpdateFixtures.Response(UpdateFixtures.Installer)));
        var prepared = await fixture.Client(handler).DownloadAsync(UpdateFixtures.Offer()); int attempts = 0;
        Assert.Null(await IntegratedUpdateInstaller.ExecuteAsync(prepared.InstallerPath, prepared.Offer, Guid.NewGuid(), _ =>
        { attempts++; throw new Win32Exception(1223); }));
        Assert.Equal(1, attempts); await File.WriteAllBytesAsync(prepared.InstallerPath, new byte[UpdateFixtures.Installer.Length]);
        var error = await Assert.ThrowsAsync<ClientFault>(() => IntegratedUpdateInstaller.ExecuteAsync(prepared.InstallerPath, prepared.Offer,
            Guid.NewGuid(), _ => { attempts++; return Task.FromResult(0); }));
        Assert.Equal("UPDATE_INTEGRITY_FAILED", error.Code); Assert.Equal(1, attempts);
    }
    [Theory]
    [InlineData(IntegratedUpdatePhase.Failed)]
    [InlineData(IntegratedUpdatePhase.Unknown)]
    [InlineData(IntegratedUpdatePhase.RestartRequired)]
    [InlineData(IntegratedUpdatePhase.Verifying)]
    [InlineData(IntegratedUpdatePhase.Installing)]
    public void NoResumeAfterUnconfirmedOrInterruptedInstallation(IntegratedUpdatePhase phase)
    {
        var fingerprint = new string('A', 64);
        var value = new IntegratedUpdateResult(Guid.NewGuid(), "0.1.0-alpha.25", phase, DateTimeOffset.UtcNow,
            ResumeConnection: true, ResumeFingerprint: fingerprint, SourceVersion: "0.1.0-alpha.24");
        Assert.False(IntegratedUpdatePolicy.CanResume(value, value.Version, fingerprint));
    }
    [Fact]
    public void ResumeRequiresExactVersionOriginalConsentAndSameConfigurationFingerprint()
    {
        var hash = new string('A', 64);
        var value = new IntegratedUpdateResult(Guid.NewGuid(), "0.1.0-alpha.25", IntegratedUpdatePhase.Succeeded, DateTimeOffset.UtcNow,
            ResumeConnection: true, ResumeFingerprint: hash, SourceVersion: "0.1.0-alpha.24");
        Assert.True(IntegratedUpdatePolicy.CanResume(value, value.Version, hash));
        Assert.False(IntegratedUpdatePolicy.CanResume(value, value.SourceVersion!, hash));
        Assert.False(IntegratedUpdatePolicy.CanResume(value, value.Version, new string('B', 64)));
        Assert.False(IntegratedUpdatePolicy.CanResume(value with { ResumeConnection = false }, value.Version, hash));
        var cancelled = value with { Phase = IntegratedUpdatePhase.Cancelled };
        Assert.True(IntegratedUpdatePolicy.CanResume(cancelled, value.SourceVersion!, hash));
        Assert.False(IntegratedUpdatePolicy.CanResume(cancelled, value.Version, hash));
    }
    [Theory]
    [InlineData("0.1.0-alpha.25", "0.1.0.25")]
    [InlineData("0.1.0-beta.2", "0.1.0.2")]
    [InlineData("0.1.0-rc.3", "0.1.0.3")]
    [InlineData("1.2.3", "1.2.3.0")]
    public void NativeFileVersionComparisonUsesReleaseFileVersion(string release, string expected) =>
        Assert.Equal(expected, IntegratedUpdatePolicy.FileVersion(release));
    [Theory]
    [InlineData("UPDATE_MAINTENANCE_UNCONFIRMED", false, TunnelPhase.Disconnected)]
    [InlineData(null, true, TunnelPhase.Disconnected)]
    [InlineData(null, false, TunnelPhase.Stopping)]
    [InlineData(null, false, TunnelPhase.Checking)]
    public void InstallationNeverUsesFaultedProtectedOrTransitionalOff(string? code, bool protection, TunnelPhase phase) =>
        Assert.False(IntegratedUpdateExecution.IsOff(new(phase, DateTimeOffset.UtcNow, Code: code, ProtectionActive: protection)));
    [Fact]
    public void OnlyKnownOwnClaimedExpiryCanFinishVerificationBeforeExplicitRelease()
    {
        var id = Guid.NewGuid(); var expired = new UpdateMaintenanceObservation(id, DateTimeOffset.UtcNow.AddSeconds(-1), true);
        var status = new TunnelStatus(TunnelPhase.Disconnected, DateTimeOffset.UtcNow, Code: "UPDATE_MAINTENANCE_UNCONFIRMED",
            ServiceVersion: "0.1.0-alpha.25", UpdateMaintenance: expired);
        Assert.False(IntegratedUpdateExecution.IsOff(status));
        const string version = "0.1.0-alpha.25";
        Assert.True(IntegratedUpdateExecution.CanCompleteVerification(status, id, version));
        Assert.False(IntegratedUpdateExecution.CanCompleteVerification(status, Guid.NewGuid(), version));
        Assert.False(IntegratedUpdateExecution.CanCompleteVerification(status with { UpdateMaintenance = expired with { Claimed = false } }, id, version));
        Assert.False(IntegratedUpdateExecution.CanCompleteVerification(status with { UpdateMaintenance = expired with { ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(1) } }, id, version));
        Assert.False(IntegratedUpdateExecution.CanCompleteVerification(status with { ProtectionActive = true }, id, version));
        Assert.False(IntegratedUpdateExecution.CanCompleteVerification(status with { ServiceVersion = "0.1.0-alpha.24" }, id, version));
        Assert.False(IntegratedUpdateExecution.CanCompleteVerification(status with { Code = "ANOTHER_FAILURE" }, id, version));
    }
}
