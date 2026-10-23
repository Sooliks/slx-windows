using System.Windows;
using Slx.Client.Core;
using Slx.Client.Desktop;
using Slx.Client.Desktop.Localization;
using Slx.Client.Desktop.ViewModels;
using Slx.Client.Windows;
using Xunit;
namespace Slx.Client.UiTests;
internal static class UpdateChecks
{
    private static readonly UpdateOffer Offer = new("0.1.0-alpha.16", UpdateSourcePolicy.ReleasePage("0.1.0-alpha.16"),
        UpdateSourcePolicy.InstallerName("0.1.0-alpha.16"),
        new("https://github.com/Sooliks/slx-windows/releases/download/v0.1.0-alpha.16/SLX-VPN-0.1.0-alpha.16-win-x64-setup.exe"), 1, new string('a', 64));
    public static void Run(MainWindow window)
    {
        var previous = window.DataContext;
        using var model = new MainViewModel(false, UiFixtures.Subscription, UiFixtures.Preferences(RoutingMode.SelectedApplications));
        var updates = model.Updates;
        window.DataContext = model;
        try
        {
            model.SendOverride = (_, _) => Task.FromResult(new PipeReply(true, "OK", new(TunnelPhase.Disconnected, DateTimeOffset.UtcNow)));
            model.PollAsync().GetAwaiter().GetResult();
            int checks = 0;
            updates.CheckOverride = (_, _) => { checks++; return Task.FromResult(new UpdateCheckResult(UpdateCheckState.Available, Offer)); };
            updates.Automatic = false;
            updates.CheckAsync(false).GetAwaiter().GetResult(); Assert.Equal(0, checks);
            updates.CheckAsync(true).GetAwaiter().GetResult(); Assert.Equal(1, checks); Assert.True(updates.ShowBanner);
            updates.LaterCommand.Execute(null); Assert.False(updates.ShowBanner);
            updates.Automatic = true; Assert.Equal(1, checks);
            updates.CheckAsync(true).GetAwaiter().GetResult(); Assert.True(updates.ShowBanner);
            model.Language = "ru"; window.Width = 1200; window.Height = 820; Sta.Pump();
            UiScreenshots.Save(window, "slx-update-available-wide");
            window.Width = 940; window.Height = 660; Sta.Pump(); UiScreenshots.Save(window, "slx-update-available-compact");
            var pending = new TaskCompletionSource<UpdateCheckResult>();
            updates.CheckOverride = (_, _) => pending.Task;
            var checking = updates.CheckAsync(true);
            Assert.True(updates.IsBusy); Assert.False(updates.CheckCommand.CanExecute(null));
            Assert.False(updates.ApplyCommand.CanExecute(null));
            pending.SetResult(new(UpdateCheckState.Unavailable)); Sta.Pump(); Assert.True(checking.IsCompletedSuccessfully);
            Assert.True(updates.ShowBanner); Assert.Equal(Text.Catalog["UpdateCheckFailed"], updates.Status);
            updates.DownloadOverride = (_, _, _) => Task.FromException<PreparedUpdate>(new ClientFault("UPDATE_INTEGRITY_FAILED"));
            updates.ApplyCommand.ExecuteAsync(null).GetAwaiter().GetResult();
            Assert.Equal(Text.Catalog["UpdateIntegrityFailed"], updates.Status);
            updates.DownloadOverride = (offer, progress, _) => { progress?.Report(100); return Task.FromResult(new PreparedUpdate(offer, @"C:\SLX UI Fixture\NeverLaunched.exe")); };
            updates.ApplyCommand.ExecuteAsync(null).GetAwaiter().GetResult(); Sta.Pump();
            Assert.Equal(Text.Catalog["UpdateReady"], updates.Status);
            Assert.Equal(Text.Catalog["UpdateActionInstall"], updates.ActionText);
            model.SendOverride = (_, _) => Task.FromResult(new PipeReply(true, "OK", new(TunnelPhase.Connected, DateTimeOffset.UtcNow, ProbePassed:true, ProtectionActive:true)));
            model.PollAsync().GetAwaiter().GetResult();
            Assert.False(updates.ApplyCommand.CanExecute(null)); Assert.Equal(Text.Catalog["UpdateDisconnectFirst"], updates.InstallHint);
            model.SendOverride = (_, _) => Task.FromResult(new PipeReply(false, "SERVICE_TIMEOUT", new(TunnelPhase.Disconnected, DateTimeOffset.UtcNow)));
            model.PollAsync().GetAwaiter().GetResult(); Assert.False(updates.ApplyCommand.CanExecute(null));
            model.SendOverride = (_, _) => Task.FromResult(new PipeReply(true, "OK", new(TunnelPhase.Disconnected, DateTimeOffset.UtcNow)));
            model.PollAsync().GetAwaiter().GetResult();
            int launches = 0;
            updates.LaunchOverride = (_, _) => { launches++; return Task.FromResult(UpdateLaunchResult.Cancelled); };
            updates.ApplyCommand.ExecuteAsync(null).GetAwaiter().GetResult();
            Assert.True(model.CanConnect); Assert.True(updates.ApplyCommand.CanExecute(null));
            Assert.Equal(Text.Catalog["UpdateInstallCancelled"], updates.Status);
            updates.LaunchOverride = (_, _) => Task.FromException<UpdateLaunchResult>(new ClientFault("UPDATE_INSTALL_FAILED"));
            updates.ApplyCommand.ExecuteAsync(null).GetAwaiter().GetResult();
            Assert.Equal(Text.Catalog["UpdateActionDownload"], updates.ActionText);
            updates.ApplyCommand.ExecuteAsync(null).GetAwaiter().GetResult();
            var launching = new TaskCompletionSource<UpdateLaunchResult>();
            updates.LaunchOverride = (_, _) => { launches++; return launching.Task; };
            var install = updates.ApplyCommand.ExecuteAsync(null);
            Assert.Equal(2, launches); Assert.False(model.CanConnect); Assert.False(model.CanEditRouting);
            model.ToggleConnectionCommand.ExecuteAsync(null).GetAwaiter().GetResult();
            launching.SetResult(UpdateLaunchResult.Started); Sta.Pump(); Assert.True(install.IsCompletedSuccessfully);
            Assert.True(updates.InstallerOpened); Assert.False(model.CanConnect); Assert.False(updates.ApplyCommand.CanExecute(null));
            updates.ApplyCommand.ExecuteAsync(null).GetAwaiter().GetResult(); Assert.Equal(2, launches);
            model.Language = "en"; model.Page = "settings"; window.Width = 1200; window.Height = 820; Sta.Pump();
            UiScreenshots.Save(window, "slx-update-settings-en");
            CheckSafeDiagnostics(model);
            CheckDisposedInstall();
            UpdatePresentationChecks.Run(window);
        }
        finally { window.DataContext = previous; Text.Catalog.SetLanguage("ru"); }
    }
    private static void CheckDisposedInstall()
    {
        var pending = new TaskCompletionSource<IDisposable>(); var lease = new Lease(); int launches = 0;
        using var updates = new UpdateViewModel("0.1.0-alpha.11", true, null, (_, _) => { }, () => true, _ => pending.Task);
        updates.CheckOverride = (_, _) => Task.FromResult(new UpdateCheckResult(UpdateCheckState.Available, Offer));
        updates.DownloadOverride = (offer, _, _) => Task.FromResult(new PreparedUpdate(offer, @"C:\SLX UI Fixture\NeverLaunched.exe"));
        updates.LaunchOverride = (_, _) => { launches++; return Task.FromResult(UpdateLaunchResult.Started); };
        updates.CheckAsync(true).GetAwaiter().GetResult(); updates.ApplyCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        var applying = updates.ApplyCommand.ExecuteAsync(null);
        updates.Dispose(); pending.SetResult(lease); Sta.Pump();
        Assert.True(applying.IsCompletedSuccessfully); Assert.Equal(0, launches); Assert.True(lease.Released);
    }
    private sealed class Lease : IDisposable
    {
        public bool Released { get; private set; }
        public void Dispose() => Released = true;
    }
    private static void CheckSafeDiagnostics(MainViewModel model)
    {
        const string code = "TRAFFIC_PROTECTION_FILTERV4_80320009";
        model.SendOverride = (_, _) => Task.FromResult(new PipeReply(false, code, new(TunnelPhase.Disconnected, DateTimeOffset.UtcNow)));
        model.PollAsync().GetAwaiter().GetResult();
        Assert.Equal(code, model.LastServiceCode);
        Assert.Contains(code, model.SafeDiagnostics);
        Assert.DoesNotContain("synthetic-ui-fixture", model.SafeDiagnostics);
        Assert.DoesNotContain("vpn.example.invalid", model.SafeDiagnostics);
        Assert.DoesNotContain("SLX UI Fixture", model.SafeDiagnostics);
        model.SendOverride = (_, _) => Task.FromResult(new PipeReply(true, "OK", new(TunnelPhase.Disconnected, DateTimeOffset.UtcNow)));
        model.PollAsync().GetAwaiter().GetResult();
        Assert.Equal(code, model.LastServiceCode);
        Assert.Equal(Text.Catalog["FaultTrafficProtection"], model.StatusDetail);
    }
}
