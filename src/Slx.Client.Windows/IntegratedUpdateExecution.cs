using System.Diagnostics;
using Slx.Client.Core;

namespace Slx.Client.Windows;

internal static class IntegratedUpdateExecution
{
    internal static bool IsOff(TunnelStatus status) => status.Phase == TunnelPhase.Disconnected && !status.ProtectionActive && status.Code is null;
    internal static async Task EnsureAdmissionAsync(IntegratedUpdateJob job, ServiceClient service)
    {
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var read = await service.SendAsync(new(PipeContract.Version, "status", Guid.NewGuid()), budget.Token);
        if (!read.Success || !IsOff(read.Status)) throw new ClientFault("UPDATE_CONNECTION_ACTIVE");
        if (read.Status.UpdateMaintenance is { } active)
        {
            if (active.SessionId != job.SessionId || active.ExpiresAt <= DateTimeOffset.UtcNow) throw new ClientFault("UPDATE_SESSION_MISMATCH");
            return;
        }
        var prepared = await service.SendAsync(new(PipeContract.Version, "prepare-update", Guid.NewGuid(), UpdateSessionId: job.SessionId), budget.Token);
        if (!prepared.Success || !IsOff(prepared.Status) || prepared.Status.UpdateMaintenance?.SessionId != job.SessionId)
            throw new ClientFault("UPDATE_MAINTENANCE_UNCONFIRMED");
    }
    internal static async Task<int?> InstallAsync(IntegratedUpdateJob job, Action started)
    {
        IntegratedUpdateStore.Validate(job, job.SessionId);
        return await IntegratedUpdateInstaller.ExecuteAsync(job.InstallerPath, job.Offer, job.SessionId, async info =>
        {
            using var installer = Process.Start(info) ?? throw new ClientFault("UPDATE_INSTALL_FAILED");
            try { started(); } catch (Exception error) when (error is IOException or UnauthorizedAccessException or ClientFault
                or System.Security.Cryptography.CryptographicException) { }
            // Once the elevated installer has started, closing a window must not interrupt the replacement.
            await installer.WaitForExitAsync(); return installer.ExitCode;
        });
    }
    internal static bool BinaryVersionsMatch(string root, string version)
    {
        string expected = IntegratedUpdatePolicy.FileVersion(version);
        foreach (var relative in new[] { "SLX VPN.exe", "SLX VPN.dll", "Slx.Client.Core.dll", "Slx.Client.Windows.dll",
            Path.Combine("service", "SLX.Service.exe"), Path.Combine("service", "SLX.Service.dll"),
            Path.Combine("service", "Slx.Client.Core.dll"), Path.Combine("service", "Slx.Client.Windows.dll"),
            Path.Combine("updater", IntegratedUpdatePaths.HostName), Path.Combine("updater", "SLX VPN Updater.dll"),
            Path.Combine("updater", "Slx.Client.Core.dll"), Path.Combine("updater", "Slx.Client.Windows.dll") })
        {
            var path = Path.Combine(root, relative); IntegratedUpdatePaths.File(path);
            var info = FileVersionInfo.GetVersionInfo(path);
            if (info.FileVersion != expected || info.ProductVersion?.Split('+')[0] != version) return false;
        }
        return true;
    }
    internal static async Task VerifyAsync(IntegratedUpdateJob job, ServiceClient service)
    {
        if (!BinaryVersionsMatch(job.InstallationRoot, job.Offer.Version)) throw new ClientFault("UPDATE_VERSION_UNCONFIRMED");
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(40));
        while (true)
        {
            try
            {
                var read = await service.SendAsync(new(PipeContract.Version, "status", Guid.NewGuid()), budget.Token);
                if (read.Success && CanCompleteVerification(read.Status, job.SessionId, job.Offer.Version)) return;
            }
            catch (ClientFault) { }
            await Task.Delay(1000, budget.Token);
        }
    }
    internal static bool CanCompleteVerification(TunnelStatus status, Guid sessionId, string version) =>
        status.ServiceVersion == version && status.Phase == TunnelPhase.Disconnected && !status.ProtectionActive
        && status.UpdateMaintenance is { } maintenance && maintenance.SessionId == sessionId
        && (status.Code is null || status.Code == "UPDATE_MAINTENANCE_UNCONFIRMED" && maintenance.Claimed && maintenance.ExpiresAt <= DateTimeOffset.UtcNow);
    internal static async Task ReleaseAsync(IntegratedUpdateJob job, ServiceClient service)
    {
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var released = await service.SendAsync(new(PipeContract.Version, "release-update", Guid.NewGuid(), UpdateSessionId: job.SessionId), budget.Token);
        if (!released.Success || !IsOff(released.Status) || released.Status.UpdateMaintenance is not null)
            throw new ClientFault("UPDATE_MAINTENANCE_UNCONFIRMED");
    }
}
