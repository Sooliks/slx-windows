using System.Buffers.Binary;
using System.Net;
using Windows.Win32;
using Windows.Win32.NetworkManagement.WindowsFilteringPlatform;
namespace Slx.Client.Windows;
public sealed unsafe partial class TrafficGuard
{
    private void AddExemptions(Guid layer,ulong luid)
    {
        var loopback=new FWPM_FILTER_CONDITION0{fieldKey=PInvoke.FWPM_CONDITION_FLAGS,matchType=FWP_MATCH_TYPE.FWP_MATCH_FLAGS_ALL_SET,
            conditionValue=new(){type=FWP_DATA_TYPE.FWP_UINT32,uint32=PInvoke.FWP_CONDITION_FLAG_IS_LOOPBACK}};
        Add(layer,true,[loopback]);
        if(luid!=0)
        {
            var tun=new FWPM_FILTER_CONDITION0{fieldKey=PInvoke.FWPM_CONDITION_IP_NEXTHOP_INTERFACE,matchType=FWP_MATCH_TYPE.FWP_MATCH_EQUAL,
                conditionValue=new(){type=FWP_DATA_TYPE.FWP_UINT64,uint64=&luid}};
            Add(layer,true,[tun],persistent:false);
        }
        string[] ranges=layer==PInvoke.FWPM_LAYER_ALE_AUTH_CONNECT_V4
            ?["0.0.0.0/8","10.0.0.0/8","100.64.0.0/10","127.0.0.0/8","169.254.0.0/16","172.16.0.0/12","192.168.0.0/16","224.0.0.0/4","240.0.0.0/4"]
            :["::/128","::1/128","fc00::/7","fe80::/10","ff00::/8"];
        foreach(var range in ranges)
        {
            var parts=range.Split('/');var address=IPAddress.Parse(parts[0]).GetAddressBytes();byte prefix=byte.Parse(parts[1],System.Globalization.CultureInfo.InvariantCulture);
            var condition=new FWPM_FILTER_CONDITION0{fieldKey=PInvoke.FWPM_CONDITION_IP_REMOTE_ADDRESS,matchType=FWP_MATCH_TYPE.FWP_MATCH_EQUAL};
            if(address.Length==4)
            {
                var mask=new FWP_V4_ADDR_AND_MASK{addr=BinaryPrimitives.ReadUInt32BigEndian(address),mask=uint.MaxValue<<(32-prefix)};
                condition.conditionValue=new(){type=FWP_DATA_TYPE.FWP_V4_ADDR_MASK,v4AddrMask=&mask};
                Add(layer,true,[condition]);
            }
            else
            {
                var mask=new FWP_V6_ADDR_AND_MASK{prefixLength=prefix};
                for(int i=0;i<16;i++)mask.addr[i]=address[i];
                condition.conditionValue=new(){type=FWP_DATA_TYPE.FWP_V6_ADDR_MASK,v6AddrMask=&mask};
                Add(layer,true,[condition]);
            }
        }
    }
    private void ApplicationFilter(Guid layer,TrafficApplicationId app)
    {
        fixed(byte* identity=app.Identity)
        {
            var application=new FWP_BYTE_BLOB{size=(uint)app.Identity.Length,data=identity};
            var condition=new FWPM_FILTER_CONDITION0{fieldKey=PInvoke.FWPM_CONDITION_ALE_APP_ID,matchType=FWP_MATCH_TYPE.FWP_MATCH_EQUAL,
                conditionValue=new(){type=FWP_DATA_TYPE.FWP_BYTE_BLOB_TYPE,byteBlob=&application}};
            Add(layer,app.Permit,[condition]);
        }
    }
    private void Add(Guid layer,bool permit,ReadOnlySpan<FWPM_FILTER_CONDITION0> conditions,bool persistent=true)
    {
        Guid provider=Provider;
        fixed(char* label="SLX VPN traffic protection")
        fixed(FWPM_FILTER_CONDITION0* entries=conditions)
        {
            var filter=new FWPM_FILTER0
            {
                filterKey=Guid.NewGuid(),providerKey=&provider,subLayerKey=Sublayer,layerKey=layer,
                flags=persistent?FWPM_FILTER_FLAGS.FWPM_FILTER_FLAG_PERSISTENT:FWPM_FILTER_FLAGS.FWPM_FILTER_FLAG_NONE,displayData=new(){name=label},
                weight=new(){type=FWP_DATA_TYPE.FWP_UINT8,uint8=permit?(byte)15:(byte)1},
                action=new(){type=permit?FWP_ACTION_TYPE.FWP_ACTION_PERMIT:FWP_ACTION_TYPE.FWP_ACTION_BLOCK},
                numFilterConditions=(uint)conditions.Length,filterCondition=entries
            };
            Check(PInvoke.FwpmFilterAdd0(engine,&filter,default,null),layer==PInvoke.FWPM_LAYER_ALE_AUTH_CONNECT_V4?TrafficOperation.FilterV4:TrafficOperation.FilterV6);
        }
    }
}
