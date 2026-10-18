namespace Slx.Client.Windows;
internal static class KnownLocations
{
    public static IEnumerable<string> Paths()
    {
        var programRoots=new[]{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)};
        string[] relative=[
            @"Google\Chrome\Application\chrome.exe",@"Yandex\YandexBrowser\Application\browser.exe",
            @"Microsoft\Edge\Application\msedge.exe",@"Mozilla Firefox\firefox.exe",
            @"BraveSoftware\Brave-Browser\Application\brave.exe",@"obs-studio\bin\64bit\obs64.exe",
            @"Steam\steam.exe",@"Epic Games\Launcher\Portal\Binaries\Win64\EpicGamesLauncher.exe"];
        foreach(var root in programRoots)foreach(var path in relative)if(!string.IsNullOrEmpty(root))yield return Path.Combine(root,path);
        var local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roaming=Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        foreach(var path in new[]{@"Google\Chrome\Application\chrome.exe",@"Yandex\YandexBrowser\Application\browser.exe",
            @"Programs\signal-desktop\Signal.exe",@"Programs\Opera\opera.exe"})
            yield return Path.Combine(local,path);
        foreach(var path in new[]{@"Telegram Desktop\Telegram.exe",@"Spotify\Spotify.exe",@"Zoom\bin\Zoom.exe"})
            yield return Path.Combine(roaming,path);
        foreach(var app in new[]{("Discord","Discord.exe"),("slack","slack.exe"),("AnthropicClaude","claude.exe")})
        {
            var root=Path.Combine(local,app.Item1);
            string[] versions;
            try{versions=Directory.Exists(root)?Directory.GetDirectories(root,"app-*").OrderDescending().Take(20).ToArray():[];}
            catch(Exception error) when(error is IOException or UnauthorizedAccessException){continue;}
            foreach(var version in versions)yield return Path.Combine(version,app.Item2);
        }
    }
}
