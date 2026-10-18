using Slx.Client.Core;
using Windows.Win32;
using Windows.Win32.NetworkManagement.WindowsFilteringPlatform;
namespace Slx.Client.Windows;
/// <summary>Persistent, provider-owned WFP policy. Dispose closes RPC only; explicit Remove releases protection.</summary>
public sealed unsafe partial class TrafficGuard : IDisposable
{
    private static readonly Guid Provider=new("511a3fb2-54a6-4502-a8a4-e14a0b793f3e");
    private static readonly Guid Sublayer=new("32d81cbf-8513-45a1-a570-044bad14c8d6");
    private FWPM_ENGINE_HANDLE engine;
    public TrafficGuard()
    {
        FWPM_ENGINE_HANDLE opened; Check(PInvoke.FwpmEngineOpen0(default,10,null,null,&opened)); engine=opened; // RPC_C_AUTHN_WINNT
        try
        {
            fixed(char* label="SLX VPN traffic protection")
            {
                var provider=new FWPM_PROVIDER0{providerKey=Provider,flags=1,displayData=new(){name=label}};
                Existing(PInvoke.FwpmProviderAdd0(engine,&provider,default));
                Guid key=Provider;
                var layer=new FWPM_SUBLAYER0{subLayerKey=Sublayer,providerKey=&key,flags=1,weight=ushort.MaxValue-10,displayData=new(){name=label}};
                Existing(PInvoke.FwpmSubLayerAdd0(engine,&layer,default));
            }
        }
        catch { Dispose();throw; }
    }
    public void Apply(ConnectPlan plan,string corePath,string servicePath,string? adapterId=null)
    {
        PlanValidator.Validate(plan);
        ulong luid=0;
        if(adapterId is not null)
        {
            if(!Guid.TryParse(adapterId,out var guid)||PInvoke.ConvertInterfaceGuidToLuid(in guid,out var value)!=0)
                throw new ClientFault("TRAFFIC_PROTECTION_FAILED");
            luid=value.Value;
        }
        Transaction(()=>
        {
            DeleteOwned();
            foreach(var layer in new[]{PInvoke.FWPM_LAYER_ALE_AUTH_CONNECT_V4,PInvoke.FWPM_LAYER_ALE_AUTH_CONNECT_V6})
            {
                AddExemptions(layer,luid);
                var dns=new FWPM_FILTER_CONDITION0{fieldKey=PInvoke.FWPM_CONDITION_IP_REMOTE_PORT,matchType=FWP_MATCH_TYPE.FWP_MATCH_EQUAL,conditionValue=new(){type=FWP_DATA_TYPE.FWP_UINT16,uint16=53}};
                Add(layer,false,[dns]);
                if(plan.Mode==RoutingMode.WholeComputer)
                {
                    ApplicationFilter(layer,corePath,true);
                    ApplicationFilter(layer,servicePath,true);
                    Add(layer,false,[]);
                }
                else foreach(var app in plan.Applications.Where(x=>x.Enabled))ApplicationFilter(layer,app.ExecutablePath,false);
            }
        });
    }
    public void Remove()=>Transaction(DeleteOwned);
    public void Dispose()
    {
        if(engine.Value!=null){PInvoke.FwpmEngineClose0(engine);engine=default;}
    }
    private void Transaction(Action action)
    {
        Check(PInvoke.FwpmTransactionBegin0(engine,0));
        try{action();Check(PInvoke.FwpmTransactionCommit0(engine));}
        catch{PInvoke.FwpmTransactionAbort0(engine);throw;}
    }
    private void DeleteOwned()
    {
        FWPM_FILTER_ENUM_HANDLE cursor;
        Check(PInvoke.FwpmFilterCreateEnumHandle0(engine,null,&cursor));
        try
        {
            while(true)
            {
                FWPM_FILTER0** entries=null;uint count=0;
                Check(PInvoke.FwpmFilterEnum0(engine,cursor,256,&entries,&count));
                try
                {
                    for(uint i=0;i<count;i++)
                    {
                        var filter=entries[i];
                        if(filter->providerKey is not null&&*filter->providerKey==Provider&&filter->subLayerKey==Sublayer)
                            Check(PInvoke.FwpmFilterDeleteByKey0(engine,&filter->filterKey));
                    }
                }
                finally{PInvoke.FwpmFreeMemory0((void**)&entries);}
                if(count<256)break;
            }
        }
        finally{PInvoke.FwpmFilterDestroyEnumHandle0(engine,cursor);}
    }
    private static void Check(uint code)
    {
        if(code!=0)throw new ClientFault("TRAFFIC_PROTECTION_FAILED");
    }
    private static void Existing(uint code){if(code!=0x80320009)Check(code);} // FWP_E_ALREADY_EXISTS
}
