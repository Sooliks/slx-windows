using Slx.Client.Core;
namespace Slx.Client.Desktop.ViewModels;
public sealed partial class MainViewModel
{
    public bool HasConnectionFacts => IsConnected && status?.ConnectedAt != null;
    public bool HasActiveProtocol => HasConnectionFacts && ActiveProtocol.Length > 0;
    public string ConnectionDuration
    {
        get
        {
            if(!HasConnectionFacts||status?.ConnectedAt is not {} start)return "";
            var elapsed=DateTimeOffset.UtcNow-start;if(elapsed<TimeSpan.Zero)elapsed=TimeSpan.Zero;
            return $"{(long)elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}";
        }
    }
    public string ActiveProtocol
    {
        get
        {
            if(!HasConnectionFacts)return "";
            var profile=Profiles.FirstOrDefault(item=>item.Name==status?.ProfileName);
            return profile?.Kind switch{"hysteria2"=>"Hysteria2","vless"=>"VLESS",_=>""};
        }
    }
    public int SelectedAppCount=>Applications.Count(item=>item.Enabled);
    public string LastConnectionCheck=>HasConnectionFacts&&status!=null?status.ObservedAt.ToLocalTime().ToString("HH:mm:ss"):"";
}
