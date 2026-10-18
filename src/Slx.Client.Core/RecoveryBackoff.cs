namespace Slx.Client.Core;
public static class RecoveryBackoff
{
    /// <summary>Retry indefinitely while intent is on, with bounded pauses and no rapid retry storm.</summary>
    public static TimeSpan Delay(int failures)=>TimeSpan.FromSeconds(failures switch{<=1=>2,2=>5,3=>10,4=>20,5=>40,_=>60});
}
