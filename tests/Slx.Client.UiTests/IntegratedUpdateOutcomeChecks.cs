using Slx.Client.Desktop.Localization;
using Slx.Client.Desktop.ViewModels;
using Xunit;
namespace Slx.Client.UiTests;
internal static class IntegratedUpdateOutcomeChecks
{
    public static void Run()
    {
        foreach (var result in new[] { UpdateApplyResult.Cancelled, UpdateApplyResult.Failed, UpdateApplyResult.RebootRequired, UpdateApplyResult.Restarting })
            CheckResult(result);
        UnknownStateDoesNotInstall();
    }
    private static void CheckResult(UpdateApplyResult result)
    {
        int calls = 0; IProgress<UpdateApplyProgress>? progress = null;
        var pending = new TaskCompletionSource<UpdateApplyResult>();
        using var updates = IntegratedUpdateChecks.Model(false, () => true, () => false, (_, resume, reporter, _) =>
        { Assert.False(resume); calls++; progress = reporter; return pending.Task; });
        IntegratedUpdateChecks.Prepare(updates);
        var applying = updates.ApplyCommand.ExecuteAsync(null);
        Assert.True(updates.IsApplying); Assert.False(updates.ApplyCommand.CanExecute(null));
        Assert.False(updates.CheckCommand.CanExecute(null)); Assert.False(updates.LaterCommand.CanExecute(null));
        foreach (var phase in new[] { "preparing", "authorizing", "installing", "verifying", "restarting" })
        {
            progress!.Report(new(phase)); Sta.Pump();
            Assert.True(updates.IsApplying); Assert.False(updates.InstallerOpened);
        }
        progress!.Report(new("https://example.invalid/private?token=synthetic-secret")); Sta.Pump();
        Assert.DoesNotContain("synthetic-secret", updates.Status);
        pending.SetResult(result); IntegratedUpdateChecks.Complete(applying);
        Assert.Equal(1, calls); Assert.False(updates.IsApplying);
        Assert.Equal(result == UpdateApplyResult.Restarting, updates.InstallerOpened);
        var expected = result switch
        {
            UpdateApplyResult.Cancelled => "UpdateInstallCancelled", UpdateApplyResult.RebootRequired => "UpdateRebootRequired",
            UpdateApplyResult.Restarting => "UpdatePhaseRestarting", _ => "UpdateIntegratedFailed"
        };
        Assert.Equal(Text.Catalog[expected], updates.Status);
        progress.Report(new("installing")); Sta.Pump(); Assert.Equal(Text.Catalog[expected], updates.Status);
        Assert.Equal(result != UpdateApplyResult.Restarting, updates.ApplyCommand.CanExecute(null));
    }
    private static void UnknownStateDoesNotInstall()
    {
        int applies = 0;
        using var updates = IntegratedUpdateChecks.Model(false, () => false, () => false, (_, _, _, _) =>
        { applies++; return Task.FromResult(UpdateApplyResult.Failed); });
        IntegratedUpdateChecks.Prepare(updates);
        Assert.False(updates.ApplyCommand.CanExecute(null)); Assert.True(updates.IsInstallBlocked);
        updates.InstallAutomatically = true; Assert.False(updates.IsInstallCountdown);
        IntegratedUpdateChecks.Complete(updates.TickCountdownAsync());
        Assert.Equal(0, applies); Assert.False(updates.ConfirmationVisible);
    }
}
