using System.IO;
using Slx.Client.Core;
using Slx.Client.Windows;
namespace Slx.Client.Desktop.ViewModels;
public sealed partial class MainViewModel
{
    internal async Task CompleteUpdateResultAsync(Guid session)
    {
        string version = VersionLabel["SLX VPN · ".Length..];
        var result = IntegratedUpdateResults.Read(session, version);
        if (result is null) return;
        CancelStartupConnection(); startupConnectionInitialized = true;
        if (result.Phase == IntegratedUpdatePhase.RestartRequired && result.InstallerExitCode == 3010)
        {
            updateInstalling = true; NotifyState();
            try
            {
                if (await IntegratedUpdateReboot.TryCompleteAsync(session, version))
                { IntegratedUpdateResults.Consume(session); Notice("UpdateCompleted"); await PollAsync(); }
                else Notice("UpdateRebootRequired");
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ClientFault)
            { Notice("UpdateResultUnconfirmed"); }
            finally { updateInstalling = false; NotifyState(); }
            return;
        }
        // Consume before any asynchronous work: a crash or lost connect reply must never replay resume.
        try { IntegratedUpdateResults.Consume(session); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ClientFault) { return; }
        updateInstalling = true; NotifyState();
        bool resume = false;
        try
        {
            using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var read = await SendControlAsync(new(PipeContract.Version, "status", Guid.NewGuid()), budget.Token);
            if (disposed) return;
            if (!read.Success) { MarkStatusUnknown(read.Code); Notice("UpdateResultUnconfirmed"); return; }
            AcceptStatus(read.Status);
            bool verified = read.Status.ServiceVersion == version && read.Status.Phase == TunnelPhase.Disconnected
                && !read.Status.ProtectionActive && read.Status.UpdateMaintenance is null && read.Status.Code is null;
            var fingerprint = UpdateFingerprint();
            resume = verified && fingerprint is not null && IntegratedUpdatePolicy.CanResume(result, version, fingerprint);
            Notice(result.Phase switch
            {
                IntegratedUpdatePhase.Succeeded when verified && result.Version == version => "UpdateCompleted",
                IntegratedUpdatePhase.Cancelled => "UpdateCancelled",
                IntegratedUpdatePhase.RestartRequired => "UpdateRebootRequired",
                _ => "UpdateResultUnconfirmed"
            });
        }
        catch (Exception error) when (error is IOException or ClientFault or OperationCanceledException)
        { if (!disposed) { MarkStatusUnknown("SERVICE_UNAVAILABLE"); Notice("UpdateResultUnconfirmed"); } }
        finally { updateInstalling = false; NotifyState(); }
        if (resume && !disposed) await ChangeTunnelAsync("connect-if-idle");
    }
}
