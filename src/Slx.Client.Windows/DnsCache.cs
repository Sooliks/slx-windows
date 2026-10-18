using System.Runtime.InteropServices;
using Slx.Client.Core;
namespace Slx.Client.Windows;
public static class DnsCache
{
    [DllImport("dnsapi.dll",ExactSpelling=true)]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool DnsFlushResolverCache();
    public static void Refresh(){if(!DnsFlushResolverCache())throw new ClientFault("DNS_CACHE_FAILED");}
}
