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
        UiFixtures.AllowSyntheticApplications(model);
        var oldPage=model.Page;var oldTheme=model.Theme;
        model.CatalogEntries=[];
        model.Discovered.Clear();
        foreach(var name in new[]{"Library Browser","Library Messenger","Library Music","Library Video","Library Editor","Library Game"})
            model.Discovered.Add(new(Guid.NewGuid(),name,@"C:\Synthetic Library\"+name+".exe"));
        model.DiscoverySearch="refresh";model.DiscoverySearch="";
        model.Page="applications";model.Theme="olive";Sta.Resize(window,1200,820);
        var icons=Sta.Descendants<Image>(window).Where(image=>image.Width==44).ToArray();
        Assert.Equal(6,icons.Length);
        Assert.All(icons,image=>Assert.NotNull(image.Source));
        Assert.Same(LocalIcon.Load(@"C:\Synthetic Library\Browser.exe"),LocalIcon.Load(@"C:\Synthetic Library\Browser.exe"));
        Assert.True(LocalIcon.Load(@"C:\Synthetic Library\Browser.exe").IsFrozen);
        Assert.NotNull(LocalIcon.Load(@"\\untrusted.invalid\share\app.exe"));
        Assert.Contains(Sta.Descendants<Button>(window),button=>ReferenceEquals(button.Command,model.AddExecutableCommand));
        Assert.Contains(Sta.Descendants<WrapPanel>(window),panel=>panel.IsItemsHost);
        UiScreenshots.Save(window,"slx-library-wide");
        Sta.Resize(window,940,660);
        Assert.All(Sta.Descendants<ScrollViewer>(window),scroll=>Assert.True(scroll.ExtentWidth<=scroll.ViewportWidth+1));
        UiScreenshots.Save(window,"slx-library-narrow");
        var first=new AppRule(Guid.NewGuid(),"Claude",@"C:\Synthetic Library\Claude\claude.exe");
        var second=new AppRule(Guid.NewGuid(),"Claude",@"C:\Synthetic Library\Claude\app-2\claude.exe");
        model.Discovered.Add(first);model.Discovered.Add(second);
        Assert.Single(model.FilteredLibrary,x=>x.Name=="Claude");
        model.AddDiscoveredCommand.Execute(first);
        var combined=Assert.Single(model.Applications,x=>x.Name=="Claude");
        Assert.Equal(2,combined.ToRules().Count());
        Assert.Contains(combined.ToRules(),x=>x.ExecutablePath==first.ExecutablePath);
        Assert.Contains(combined.ToRules(),x=>x.ExecutablePath==second.ExecutablePath);
        combined.Enabled=false;Assert.All(combined.ToRules(),x=>Assert.False(x.Enabled));
        combined.Enabled=true;Assert.All(combined.ToRules(),x=>Assert.True(x.Enabled));
        using(var loaded=new MainViewModel(false,null,new ClientPreferences(Applications:combined.ToRules().ToList())))
        {Assert.Single(loaded.Applications);Assert.Equal(2,loaded.Applications[0].ToRules().Count());}
        model.RemoveAppCommand.Execute(combined);
        model.Page=oldPage;model.Theme=oldTheme;Sta.Pump();
    }
}
