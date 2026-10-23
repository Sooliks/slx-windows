using Slx.Client.Core;
using Slx.Client.Desktop.Localization;
using Slx.Client.Desktop.ViewModels;
using Slx.Client.Windows;
using Xunit;
namespace Slx.Client.UiTests;
internal static class UpdateReuseChecks
{
    private static readonly UpdateOffer Offer = new("0.1.0-alpha.16", UpdateSourcePolicy.ReleasePage("0.1.0-alpha.16"),
        UpdateSourcePolicy.InstallerName("0.1.0-alpha.16"),
        new("https://github.com/Sooliks/slx-windows/releases/download/v0.1.0-alpha.16/SLX-VPN-0.1.0-alpha.16-win-x64-setup.exe"), 1, new string('a', 64));
    // A verified installer staged earlier for the offered release goes straight to Install: no second download.
    public static void Run()
    {
        int downloads = 0, lookups = 0;
        using var updates = new UpdateViewModel("0.1.0-alpha.15", true, null, (_, _) => { }, () => true, _ => Task.FromResult<IDisposable>(new Lease()));
        updates.CheckOverride = (_, _) => Task.FromResult(new UpdateCheckResult(UpdateCheckState.Available, Offer));
        updates.DownloadOverride = (_, _, _) => { downloads++; return Task.FromException<PreparedUpdate>(new ClientFault("UPDATE_DOWNLOAD_FAILED")); };
        updates.FindPreparedOverride = (offer, _) => { lookups++; return Task.FromResult<PreparedUpdate?>(new PreparedUpdate(offer, @"C:\SLX UI Fixture\Staged.exe")); };
        updates.CheckAsync(true).GetAwaiter().GetResult(); Sta.Pump();
        Assert.Equal(1, lookups); Assert.Equal(0, downloads);
        Assert.Equal(Text.Catalog["UpdateReady"], updates.Status);
        Assert.Equal(Text.Catalog["UpdateActionInstall"], updates.ActionText);
        updates.CheckAsync(true).GetAwaiter().GetResult();
        Assert.Equal(1, lookups);
        using var fresh = new UpdateViewModel("0.1.0-alpha.15", true, null, (_, _) => { }, () => true, _ => Task.FromResult<IDisposable>(new Lease()));
        fresh.CheckOverride = updates.CheckOverride;
        fresh.FindPreparedOverride = (_, _) => Task.FromResult<PreparedUpdate?>(null);
        fresh.CheckAsync(true).GetAwaiter().GetResult();
        Assert.Equal(Text.Catalog["UpdateFound"], fresh.Status); Assert.Equal(Text.Catalog["UpdateActionDownload"], fresh.ActionText);
    }
    private sealed class Lease : IDisposable { public void Dispose() { } }
}
