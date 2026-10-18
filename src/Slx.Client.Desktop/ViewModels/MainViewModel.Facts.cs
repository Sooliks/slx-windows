using Slx.Client.Core;
namespace Slx.Client.Desktop.ViewModels;
public sealed partial class MainViewModel
{
    public string ConnectionDuration
    {
        get
        {
            if(!IsConnected||status?.ConnectedAt is not {} start)return "—";
            var elapsed=DateTimeOffset.UtcNow-start;if(elapsed<TimeSpan.Zero)elapsed=TimeSpan.Zero;
            return $"{(long)elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}";
        }
    }
    public string ActiveProtocol
    {
        get
        {
            if(!IsConnected)return "—";
            var profile=Profiles.FirstOrDefault(item=>item.Name==status?.ProfileName);
            return profile?.Kind switch{"hysteria2"=>"Hysteria2","vless"=>"VLESS",_=>"—"};
        }
    }
    public int SelectedAppCount=>Applications.Count(item=>item.Enabled);
    public string LastConnectionCheck=>statusFresh&&status!=null?status.ObservedAt.ToLocalTime().ToString("HH:mm:ss"):"—";
}
