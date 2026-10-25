using System.Globalization;
using Slx.Client.Core;
using Slx.Client.Windows;
using Xunit;
namespace Slx.Client.Tests;
public sealed class LibraryApplicationOrderTests
{
    private static AppRule App(string name,string file,bool enabled=true)=>new(Guid.NewGuid(),name,@"C:\Library fixture\"+file,enabled);
    [Fact]
    public void BrowsersTelegramDiscordAndGamesPrecedeOtherInstalledEntries()
    {
        var editor=App("A editor","editor.exe");var game=App("B game","cs2.exe",false);
        var discord=App("C Discord","DISCORD.EXE");var telegram=App("D Telegram","Telegram.exe");
        var browser=App("Z browser","ChRoMe.ExE");var input=new[]{editor,game,discord,telegram,browser};
        Assert.Equal(new[]{browser,telegram,discord,game,editor},new LibraryApplicationOrder([]).Order(input));
        Assert.False(game.Enabled);Assert.Same(game,input[1]);
    }
    [Theory]
    [InlineData(@"D:/Steam/steamapps/common/Example/game.exe",3)]
    [InlineData(@"D:\Steam\STEAMAPPS\COMMON\Example\game.exe",3)]
    [InlineData(@"D:\Epic Games\Example\Example.exe",3)]
    [InlineData(@"D:\Steam\steamapps\common-copy\editor.exe",4)]
    [InlineData(@"D:\Not Epic Games\editor.exe",4)]
    [InlineData(@"D:\Tools\telegram-helper.exe",4)]
    [InlineData(@"D:\Chrome\update.exe",4)]
    [InlineData(@"D:\Tools\not-discord.exe",4)]
    public void OnlyExactExecutableOrKnownGamePathChangesPresentationRank(string path,int expected)
        =>Assert.Equal(expected,new LibraryApplicationOrder([]).Priority(new AppRule(Guid.NewGuid(),"Unrelated label",path)));
    [Fact]
    public void CurrentCatalogCanRankNewBrowserOrGameWithoutAddingAnApplication()
    {
        CatalogApplication[] catalog=[new("custom-browser","Custom Browser","browser",["custom.exe"]),
            new("custom-game","Custom Game","game",["newgame.exe"])];
        var order=new LibraryApplicationOrder(catalog);var browser=App("Z custom","CUSTOM.EXE");
        var game=App("Y game","newgame.exe");var input=new[]{App("A editor","editor.exe"),game,browser};
        Assert.Equal(new[]{browser,game,input[0]},order.Order(input));
        Assert.Equal(3,order.Order(input).Count());Assert.Equal(0,order.Priority(catalog[0]));
    }
    [Fact]
    public void PriorityIsAppliedBeforeTheDiscoveryLimit()
    {
        var input=Enumerable.Range(0,300).Select(index=>App("A tool "+index,"tool"+index+".exe"))
            .Append(App("Z Telegram","Telegram.exe")).ToArray();
        var result=new LibraryApplicationOrder([]).Order(input).Take(300).ToArray();
        Assert.Same(input[^1],result[0]);Assert.Equal(300,result.Length);
    }
    [Fact]
    public async Task IndependentScannerPublishesPreferredInstalledEntryBeforeItsLimit()
    {
        var input=Enumerable.Range(0,300).Select(index=>App("A tool "+index,"tool"+index+".exe"))
            .Append(App("Z Discord","Discord.exe")).ToArray();
        DiscoveryBatch? result=null;
        await DiscoveryScanner.RunAsync([new DiscoverySourceScan(DiscoverySource.Installed,_=>input)],
            batch=>result=batch,CancellationToken.None);
        Assert.NotNull(result);Assert.True(result.Success);Assert.Equal(300,result.Apps.Count);
        Assert.Same(input[^1],result.Apps[0]);
    }
    [Fact]
    public void OrderingDoesNotDependOnDiscoveryArrivalOrCurrentLanguage()
    {
        var previous=CultureInfo.CurrentCulture;
        try
        {
            var input=new[]{App("İ tool","z.exe"),App("I tool","a.exe"),App("same","z.exe"),App("Same","a.exe"),App("Z browser","firefox.exe")};
            CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("en-US");
            var expected=new LibraryApplicationOrder([]).Order(input).ToArray();
            CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("tr-TR");
            Assert.Equal(expected,new LibraryApplicationOrder([]).Order(input.Reverse()));
        }
        finally{CultureInfo.CurrentCulture=previous;}
    }
}
