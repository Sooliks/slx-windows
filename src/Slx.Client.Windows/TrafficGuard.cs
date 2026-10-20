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
        FWPM_ENGINE_HANDLE opened; Check(PInvoke.FwpmEngineOpen0(default,10,null,null,&opened),TrafficOperation.Open); engine=opened; // RPC_C_AUTHN_WINNT
        try
        {
            fixed(char* label="SLX VPN traffic protection")
            {
                var provider=new FWPM_PROVIDER0{providerKey=Provider,flags=1,displayData=new(){name=label}};
                Existing(PInvoke.FwpmProviderAdd0(engine,&provider,default),TrafficOperation.Provider);
                Guid key=Provider;
                var layer=new FWPM_SUBLAYER0{subLayerKey=Sublayer,providerKey=&key,flags=1,weight=ushort.MaxValue-10,displayData=new(){name=label}};
                Existing(PInvoke.FwpmSubLayerAdd0(engine,&layer,default),TrafficOperation.Sublayer);
            }
        }
        catch { Dispose();throw; }
    }
    public void Apply(ConnectPlan plan,string corePath,string servicePath,string? adapterId=null)
    {
        PlanValidator.Validate(plan);
        var applications=TrafficApplicationIds.Resolve(plan,corePath,servicePath);
        ulong luid=0;
        if(adapterId is not null)
        {
            if(!Guid.TryParse(adapterId,out var guid))throw new ClientFault("TRAFFIC_PROTECTION_ADAPTER_INVALID");
            Check((uint)PInvoke.ConvertInterfaceGuidToLuid(in guid,out var value),TrafficOperation.Adapter);
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
                    foreach(var app in applications)ApplicationFilter(layer,app);
                    Add(layer,false,[]);
                }
                else foreach(var app in applications)ApplicationFilter(layer,app);
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
        Check(PInvoke.FwpmTransactionBegin0(engine,0),TrafficOperation.Begin);
        try{action();Check(PInvoke.FwpmTransactionCommit0(engine),TrafficOperation.Commit);}
        catch{PInvoke.FwpmTransactionAbort0(engine);throw;}
    }
    private void DeleteOwned()
    {
        foreach(var layer in new[]{PInvoke.FWPM_LAYER_ALE_AUTH_CONNECT_V4,PInvoke.FWPM_LAYER_ALE_AUTH_CONNECT_V6})DeleteOwned(layer);
    }
    private void DeleteOwned(Guid layer)
    {
        Guid provider=Provider;
        var template=new FWPM_FILTER_ENUM_TEMPLATE0{providerKey=&provider,layerKey=layer,actionMask=uint.MaxValue,
            flags=PInvoke.FWP_FILTER_ENUM_FLAG_INCLUDE_DISABLED};
        FWPM_FILTER_ENUM_HANDLE cursor;
        Check(PInvoke.FwpmFilterCreateEnumHandle0(engine,&template,&cursor),TrafficOperation.Enumerate);
        try
        {
            while(true)
            {
                FWPM_FILTER0** entries=null;uint count=0;
                Check(PInvoke.FwpmFilterEnum0(engine,cursor,256,&entries,&count),TrafficOperation.Enumerate);
                try
                {
                    for(uint i=0;i<count;i++)
                    {
                        var filter=entries[i];
                        if(filter->providerKey is not null&&*filter->providerKey==Provider&&filter->subLayerKey==Sublayer)
                            Check(PInvoke.FwpmFilterDeleteByKey0(engine,&filter->filterKey),TrafficOperation.Delete);
                    }
                }
                finally{PInvoke.FwpmFreeMemory0((void**)&entries);}
                if(count<256)break;
            }
        }
        finally{PInvoke.FwpmFilterDestroyEnumHandle0(engine,cursor);}
    }
    private static void Check(uint code,TrafficOperation operation)=>TrafficProtectionError.Check(code,operation);
    private static void Existing(uint code,TrafficOperation operation){if(code!=0x80320009)Check(code,operation);} // FWP_E_ALREADY_EXISTS
}
