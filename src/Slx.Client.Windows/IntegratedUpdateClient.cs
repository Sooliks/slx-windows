using System.Diagnostics;
using System.Reflection;
using Slx.Client.Core;

namespace Slx.Client.Windows;

public sealed class IntegratedUpdateClient
{
    public async Task<IntegratedUpdateHandoff> StageAndLaunchAsync(PreparedUpdate prepared, Guid sessionId,
        bool resumeConnection, string language, CancellationToken cancellationToken = default, string? resumeFingerprint = null)
    {
        UpdateSourcePolicy.Validate(prepared.Offer);
        string installation = IntegratedUpdatePaths.ValidateInstallation(AppContext.BaseDirectory);
        using var current = Process.GetCurrentProcess();
        string sourceVersion = typeof(IntegratedUpdateClient).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
            .InformationalVersion.Split('+')[0];
        var job = new IntegratedUpdateJob(sessionId, prepared.Offer, prepared.InstallerPath, installation, current.Id,
            current.StartTime.ToUniversalTime(), DateTimeOffset.UtcNow, language, resumeConnection, resumeFingerprint, sourceVersion);
        IntegratedUpdateStore.Validate(job, sessionId); IntegratedUpdatePaths.Create(sessionId);
        Process? child = null; bool committed = false;
        try
        {
            await IntegratedUpdateStaging.CopyAsync(installation, sessionId, cancellationToken);
            IntegratedUpdateStore.Write(sessionId, "job.bin", job);
            IntegratedUpdateStore.Write(sessionId, "result.bin", new IntegratedUpdateResult(sessionId, prepared.Offer.Version,
                IntegratedUpdatePhase.Staged, DateTimeOffset.UtcNow, ResumeConnection: resumeConnection,
                ResumeFingerprint: resumeFingerprint, SourceVersion: sourceVersion));
            using var ready = new EventWaitHandle(false, EventResetMode.ManualReset, IntegratedUpdatePaths.ReadyName(sessionId));
            string helper = Path.Combine(IntegratedUpdatePaths.Session(sessionId), "host", IntegratedUpdatePaths.HostName);
            child = Process.Start(new ProcessStartInfo(helper) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(helper)!,
                ArgumentList = { "--session", sessionId.ToString("N") } }) ?? throw new ClientFault("UPDATE_HANDOFF_FAILED");
            using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); budget.CancelAfter(TimeSpan.FromSeconds(30));
            while (!ready.WaitOne(0))
            {
                if (child.HasExited) throw new ClientFault("UPDATE_HANDOFF_FAILED");
                await Task.Delay(100, budget.Token);
            }
            var identity = IntegratedUpdateStore.Read<IntegratedUpdateHostIdentity>(sessionId, "host.bin");
            if (identity.ProcessId != child.Id || identity.StartedAt != new DateTimeOffset(child.StartTime.ToUniversalTime()) || child.HasExited)
                throw new ClientFault("UPDATE_HANDOFF_FAILED");
            cancellationToken.ThrowIfCancellationRequested();
            // The helper may observe a later ordinary UI exit after a cancelled handoff; only this commit grants installation.
            IntegratedUpdateStore.Write(sessionId, "handoff.bin", identity);
            committed = true;
            return new(sessionId, child.Id);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { throw new ClientFault("UPDATE_HANDOFF_FAILED"); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception
            or System.Text.Json.JsonException or System.Security.Cryptography.CryptographicException or ArgumentException)
        { throw new ClientFault("UPDATE_HANDOFF_FAILED"); }
        finally
        {
            if (child is not null && !committed) await StopUncommittedChildAsync(child);
            child?.Dispose();
        }
    }
    private static async Task StopUncommittedChildAsync(Process child)
    {
        // This exact created helper cannot launch an installer before this still-running UI commits its handoff.
        // Never terminate a process tree, an installer, or any process from a stored receipt.
        try
        {
            if (!child.HasExited) child.Kill();
            using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await child.WaitForExitAsync(budget.Token);
        }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException or OperationCanceledException
            or NotSupportedException) { }
    }
}
