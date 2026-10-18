using System.Windows;
using System.Windows.Controls;
using Slx.Client.Core;
using Slx.Client.Desktop;
using Slx.Client.Desktop.Presentation;
using Slx.Client.Desktop.ViewModels;
using Xunit;
namespace Slx.Client.UiTests;
internal static class LibraryChecks
{
    public static void Run(MainWindow window)
    {
        var model=(MainViewModel)window.DataContext;
        var oldPage=model.Page;var oldTheme=model.Theme;
        model.Discovered.Clear();
        foreach(var name in new[]{"Browser","Messenger","Music","Video","Editor","Game"})
            model.Discovered.Add(new(Guid.NewGuid(),name,@"C:\Synthetic Library\"+name+".exe"));
        model.DiscoverySearch="refresh";model.DiscoverySearch="";
        model.Page="applications";model.Theme="olive";window.Width=1200;window.Height=820;Sta.Pump();
        var icons=Sta.Descendants<Image>(window).Where(image=>image.Width==44).ToArray();
        Assert.Equal(6,icons.Length);
        Assert.All(icons,image=>Assert.NotNull(image.Source));
        Assert.Same(LocalIcon.Load(@"C:\Synthetic Library\Browser.exe"),LocalIcon.Load(@"C:\Synthetic Library\Browser.exe"));
        Assert.True(LocalIcon.Load(@"C:\Synthetic Library\Browser.exe").IsFrozen);
        Assert.NotNull(LocalIcon.Load(@"\\untrusted.invalid\share\app.exe"));
        Assert.Contains(Sta.Descendants<Button>(window),button=>ReferenceEquals(button.Command,model.AddExecutableCommand));
        Assert.Contains(Sta.Descendants<WrapPanel>(window),panel=>panel.IsItemsHost);
        UiScreenshots.Save(window,"slx-library-wide");
        window.Width=940;window.Height=660;Sta.Pump();
        Assert.All(Sta.Descendants<ScrollViewer>(window),scroll=>Assert.True(scroll.ExtentWidth<=scroll.ViewportWidth+1));
        UiScreenshots.Save(window,"slx-library-narrow");
        model.Page=oldPage;model.Theme=oldTheme;Sta.Pump();
    }
}
