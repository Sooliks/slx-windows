namespace Slx.Client.Core;
public static class RecoveryBackoff
{
    /// <summary>Retry indefinitely while intent is on, with bounded pauses and no rapid retry storm.</summary>
    public static TimeSpan Delay(int failures)=>TimeSpan.FromSeconds(failures switch{<=1=>2,2=>5,3=>10,4=>20,5=>40,_=>60});
    /// <summary>Tunnel recovery restarts only the VPN core, so short pauses notice a recovered server within seconds.</summary>
    public static TimeSpan Reconnect(int failures)=>TimeSpan.FromSeconds(failures switch{<=1=>1,2=>2,3=>3,4=>5,5=>8,_=>10});
}
