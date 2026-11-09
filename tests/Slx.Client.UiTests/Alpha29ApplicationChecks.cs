using System.Windows;
using System.Windows.Controls;
using Slx.Client.Core;
using Slx.Client.Desktop;
using Slx.Client.Desktop.Localization;
using Slx.Client.Desktop.ViewModels;
using Xunit;
namespace Slx.Client.UiTests;

internal static class Alpha29ApplicationChecks
{
    internal static void Run(Window owner)
    {
        string shared = @"C:\Synthetic Riot\RiotClientServices.exe";
        AppRule[] original = [new(Guid.NewGuid(), "League of Legends", shared, false),
            new(Guid.NewGuid(), "Riot helper", @"C:\Synthetic Riot\RiotClientUx.exe", true)];
        using (var existing = new MainViewModel(false, UiFixtures.Subscription, new ClientPreferences(Applications: original.ToList())))
            Assert.Equal(original, existing.Applications.SelectMany(x => x.ToRules()).ToArray());
        using var model = new MainViewModel(false, UiFixtures.Subscription, new ClientPreferences(Applications:
            [new(Guid.NewGuid(), "League of Legends", shared, false)]));
        // Use a second ordinary bundle sharing one exact path without silently adding any game family.
        var other = new AppItemViewModel([new(Guid.NewGuid(), "Enabled bundle", @"C:\Synthetic Riot\Bundle.exe", true),
            new(Guid.NewGuid(), "Enabled bundle", shared, true)]);
        model.Applications.Add(other);
        var riot = model.Applications.First(x => x.Name == "Riot Client");
        riot.Enabled = true; riot.Enabled = false;
        Assert.False(riot.Enabled); Assert.NotEmpty(riot.SharedWarning);
        Assert.Contains("Riot", riot.GroupDescription);
        model.DiscoverySearch = "riot";
        var added = Assert.Single(model.FilteredLibrary, x => x.Name == "Riot Client");
        Assert.True(added.IsAdded); int count = model.Applications.Count;
        model.AddLibraryCommand.Execute(added);
        Assert.Equal(count, model.Applications.Count); Assert.False(riot.Enabled); Assert.Contains(riot, model.FilteredApplications);
        model.Page = "applications";
        var window = new MainWindow(model, true) { Owner = owner, ShowInTaskbar = false, Left = -20000, Top = -20000 };
        window.Show();
        try
        {
            foreach (string language in new[] { "ru", "en" })
            {
                model.Language = language; Sta.Resize(window, 940, 760);
                Assert.Contains(Sta.Descendants<TextBlock>(window), x => x.IsVisible && x.Text == Text.Catalog["RiotSharedClient"]);
                Assert.Contains(Sta.Descendants<TextBlock>(window), x => x.IsVisible && x.Text == riot.SharedWarning);
                Assert.All(Sta.Descendants<ScrollViewer>(window), scroll => Assert.True(scroll.ExtentWidth <= scroll.ViewportWidth + 1));
                UiScreenshots.Save(window, "slx-alpha29-applications-" + language);
                var warning = Sta.Descendants<TextBlock>(window).First(x => x.IsVisible && x.Text == riot.SharedWarning);
                warning.BringIntoView(); Sta.Pump();
                UiScreenshots.Save(window, "slx-alpha29-shared-executable-" + language);
                foreach (var scroll in Sta.Descendants<ScrollViewer>(window)) scroll.ScrollToTop();
                Sta.Pump();
            }
        }
        finally { window.Close(); }
    }
}
