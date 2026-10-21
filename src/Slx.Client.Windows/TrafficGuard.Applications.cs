using Slx.Client.Core;
using Windows.Win32;
namespace Slx.Client.Windows;
public sealed unsafe partial class TrafficGuard
{
    private TrafficApplicationState? applicationState;
    public TrafficApplicationPolicy CurrentApplications=>applicationState?.Current
        ??throw new ClientFault("TRAFFIC_PROTECTION_APPLICATION_BASELINE");
    public TrafficApplicationPolicy PrepareApplications(ConnectPlan plan)
    {
        PlanValidator.Validate(plan);
        if(plan.Mode!=RoutingMode.SelectedApplications)throw new ClientFault("TRAFFIC_PROTECTION_APPLICATION_MODE");
        _ = CurrentApplications;
        return TrafficApplicationPolicy.FromIdentities(TrafficApplicationIds.Resolve(plan,"","").Select(app=>app.Identity));
    }
    public void ApplyApplications(TrafficApplicationPolicy target)
    {
        var baseline=applicationState??throw new ClientFault("TRAFFIC_PROTECTION_APPLICATION_BASELINE");
        baseline.Apply(target,update=>Transaction(()=>
        {
            foreach(var app in update.Added)
            {
                ApplicationFilter(PInvoke.FWPM_LAYER_ALE_AUTH_CONNECT_V4,new(app.Identity,false),app.V4);
                ApplicationFilter(PInvoke.FWPM_LAYER_ALE_AUTH_CONNECT_V6,new(app.Identity,false),app.V6);
            }
            foreach(var app in update.Removed)
            {
                Guid v4=app.V4,v6=app.V6;
                Check(PInvoke.FwpmFilterDeleteByKey0(engine,&v4),TrafficOperation.Delete);
                Check(PInvoke.FwpmFilterDeleteByKey0(engine,&v6),TrafficOperation.Delete);
            }
        }));
    }
}
