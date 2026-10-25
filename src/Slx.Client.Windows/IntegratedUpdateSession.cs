using System.Diagnostics;
using Slx.Client.Core;

namespace Slx.Client.Windows;

/// <summary>Runs without elevation; only the verified native installer is passed to Windows UAC.</summary>
public sealed class IntegratedUpdateSession
{
    private readonly IntegratedUpdateJob job;
    private readonly ServiceClient service = new();
    private bool running;
    public string Language => job.Language;
    public string TargetVersion => job.Offer.Version;
    public IntegratedUpdateSession(Guid id)
    {
        job = IntegratedUpdateStore.Read<IntegratedUpdateJob>(id, "job.bin"); IntegratedUpdateStore.Validate(job, id);
    }
    public void SignalReady()
    {
        using var process = Process.GetCurrentProcess();
        IntegratedUpdateStore.Write(job.SessionId, "host.bin", new IntegratedUpdateHostIdentity(process.Id, process.StartTime.ToUniversalTime()));
        using var ready = new EventWaitHandle(false, EventResetMode.ManualReset, IntegratedUpdatePaths.ReadyName(job.SessionId)); ready.Set();
    }
    public async Task<IntegratedUpdateResult> RunAsync(IProgress<IntegratedUpdatePhase>? progress = null)
    {
        if (running) throw new ClientFault("UPDATE_HANDOFF_FAILED");
        running = true;
        DateTimeOffset? bootStartedAt = null;
        IntegratedUpdateResult Save(IntegratedUpdatePhase phase, int? code = null)
        {
            progress?.Report(phase);
            var value = new IntegratedUpdateResult(job.SessionId, job.Offer.Version, phase, DateTimeOffset.UtcNow, code,
                job.ResumeConnection, job.ResumeFingerprint, job.SourceVersion, bootStartedAt);
            IntegratedUpdateStore.Write(job.SessionId, "result.bin", value); return value;
        }
        try
        {
            Save(IntegratedUpdatePhase.WaitingForApplication);
            await WaitForOriginalAsync();
            using (var current = Process.GetCurrentProcess())
            {
                var committed = IntegratedUpdateStore.Read<IntegratedUpdateHostIdentity>(job.SessionId, "handoff.bin");
                if (committed.ProcessId != current.Id || committed.StartedAt != new DateTimeOffset(current.StartTime.ToUniversalTime()))
                    throw new ClientFault("UPDATE_HANDOFF_FAILED");
            }
            await IntegratedUpdateExecution.EnsureAdmissionAsync(job, service);
            Save(IntegratedUpdatePhase.AwaitingPermission);
            var code = await IntegratedUpdateExecution.InstallAsync(job, () => Save(IntegratedUpdatePhase.Installing));
            if (code is null)
            { await ReleaseIfUnchangedAsync(); return Save(IntegratedUpdatePhase.Cancelled); }
            if (code == 3010)
            {
                bootStartedAt = await IntegratedUpdateBoot.ReadAsync();
                return Save(IntegratedUpdatePhase.RestartRequired, code);
            }
            if (code != 0) { await ReleaseIfUnchangedAsync(); return Save(IntegratedUpdatePhase.Failed, code); }
            Save(IntegratedUpdatePhase.Verifying);
            await IntegratedUpdateExecution.VerifyAsync(job, service);
            await IntegratedUpdateExecution.ReleaseAsync(job, service);
            return Save(IntegratedUpdatePhase.Succeeded, code);
        }
        catch (Exception error) when (error is ClientFault or IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception
            or InvalidOperationException or OperationCanceledException or System.Security.Cryptography.CryptographicException or System.Text.Json.JsonException)
        {
            await ReleaseIfUnchangedAsync();
            try { return Save(IntegratedUpdatePhase.Failed); }
            catch (Exception storage) when (storage is ClientFault or IOException or UnauthorizedAccessException or System.Security.Cryptography.CryptographicException)
            {
                // A full disk cannot turn a known process result into an unhandled UI crash or a successful resume receipt.
                progress?.Report(IntegratedUpdatePhase.Failed);
                return new(job.SessionId, job.Offer.Version, IntegratedUpdatePhase.Failed, DateTimeOffset.UtcNow, SourceVersion: job.SourceVersion);
            }
        }
        finally { running = false; }
    }
    private async Task WaitForOriginalAsync()
    {
        Process process;
        try { process = Process.GetProcessById(job.OriginalProcessId); } catch (ArgumentException) { return; }
        using (process)
        {
            if (new DateTimeOffset(process.StartTime.ToUniversalTime()) != job.OriginalProcessStartedAt) return;
            using var budget = new CancellationTokenSource(TimeSpan.FromMinutes(2)); await process.WaitForExitAsync(budget.Token);
        }
    }
    private async Task ReleaseIfUnchangedAsync()
    {
        try
        {
            if (IntegratedUpdateExecution.BinaryVersionsMatch(job.InstallationRoot, job.SourceVersion))
            {
                using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                var read = await service.SendAsync(new(PipeContract.Version, "status", Guid.NewGuid()), budget.Token);
                if (read.Success && read.Status.ServiceVersion == job.SourceVersion && IntegratedUpdateExecution.IsOff(read.Status))
                    await IntegratedUpdateExecution.ReleaseAsync(job, service);
            }
        }
        catch (Exception error) when (error is ClientFault or IOException or UnauthorizedAccessException or OperationCanceledException) { }
    }
    public bool ReturnToApplication()
    {
        // No executable, arguments or working directory are accepted from a result receipt.
        string root = IntegratedUpdatePaths.ValidateInstallation(job.InstallationRoot);
        using var process = Process.Start(new ProcessStartInfo(Path.Combine(root, "SLX VPN.exe"))
        { UseShellExecute = false, WorkingDirectory = root, ArgumentList = { "--update-result", job.SessionId.ToString("N") } });
        return process is not null;
    }
}
