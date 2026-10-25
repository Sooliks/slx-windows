using Slx.Client.Core;

namespace Slx.Client.Windows;

public static class IntegratedUpdateReboot
{
    /// <summary>Completes a known 3010 only after a changed OS start observation and fresh exact installation/service checks.
    /// It never starts the VPN, retries the installer, consumes the receipt or restarts Windows.</summary>
    public static async Task<bool> TryCompleteAsync(Guid id, string currentVersion, CancellationToken cancellationToken = default)
    {
        try
        {
            var value = IntegratedUpdateResults.Read(id, currentVersion);
            if (value is null) return false;
            var job = IntegratedUpdateStore.Read<IntegratedUpdateJob>(id, "job.bin");
            var service = new ServiceClient();
            return await TryCompleteAsync(value, currentVersion, IntegratedUpdateBoot.ReadAsync,
                () => IntegratedUpdateExecution.BinaryVersionsMatch(IntegratedUpdatePaths.ValidateInstallation(job.InstallationRoot), currentVersion),
                service.SendAsync, cancellationToken);
        }
        catch (Exception error) when (error is ClientFault or IOException or UnauthorizedAccessException or OperationCanceledException
            or ArgumentException or System.Security.Cryptography.CryptographicException or System.Text.Json.JsonException) { return false; }
    }
    internal static async Task<bool> TryCompleteAsync(IntegratedUpdateResult value, string currentVersion,
        Func<CancellationToken, Task<DateTimeOffset?>> boot, Func<bool> binariesMatch,
        Func<PipeRequest, CancellationToken, Task<PipeReply>> send, CancellationToken cancellationToken = default)
    {
        if (value.Phase != IntegratedUpdatePhase.RestartRequired || value.InstallerExitCode != 3010
            || value.Version != currentVersion || value.BootStartedAt is null || value.SessionId == Guid.Empty) return false;
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            if (!IntegratedUpdateBoot.Different(value.BootStartedAt, await boot(budget.Token)) || !binariesMatch()) return false;
            var read = await send(new(PipeContract.Version, "status", Guid.NewGuid()), budget.Token).WaitAsync(budget.Token);
            if (!read.Success || read.Status.UpdateMaintenance is not { Claimed: true }
                || !IntegratedUpdateExecution.CanCompleteVerification(read.Status, value.SessionId, currentVersion)) return false;
            var released = await send(new(PipeContract.Version, "release-update", Guid.NewGuid(), UpdateSessionId: value.SessionId), budget.Token)
                .WaitAsync(budget.Token);
            return released.Success && released.Status.ServiceVersion == currentVersion
                && IntegratedUpdateExecution.IsOff(released.Status) && released.Status.UpdateMaintenance is null;
        }
        catch (Exception error) when (error is ClientFault or IOException or UnauthorizedAccessException or OperationCanceledException
            or InvalidOperationException) { return false; }
    }
}
