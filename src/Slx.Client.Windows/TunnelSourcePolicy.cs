using System.Buffers.Binary;
using System.Net;
using System.Runtime.InteropServices;
using Slx.Client.Core;
namespace Slx.Client.Windows;
/// <summary>Only the recorded owned TUN address; no global IPv6 or physical-interface changes.</summary>
public static class TunnelSourcePolicy
{
    [StructLayout(LayoutKind.Explicit, Size=28)]
    private struct SocketAddress
    {
        [FieldOffset(0)] public ushort Family;
        [FieldOffset(8)] public ulong First;
        [FieldOffset(16)] public ulong Last;
    }
    [StructLayout(LayoutKind.Explicit, Size=80)]
    private struct AddressRow
    {
        [FieldOffset(0)] public SocketAddress Address;
        [FieldOffset(32)] public ulong Luid;
        [FieldOffset(40)] public uint Index;
        [FieldOffset(60)] public byte PrefixLength;
        [FieldOffset(61)] public byte SkipAsSource;
    }
    [DllImport("iphlpapi.dll", ExactSpelling=true)]
    private static extern uint ConvertInterfaceGuidToLuid(ref Guid guid,out ulong luid);
    [DllImport("iphlpapi.dll", ExactSpelling=true)]
    private static extern void InitializeUnicastIpAddressEntry(out AddressRow row);
    [DllImport("iphlpapi.dll", ExactSpelling=true)]
    private static extern uint GetUnicastIpAddressEntry(ref AddressRow row);
    [DllImport("iphlpapi.dll", ExactSpelling=true)]
    private static extern uint SetUnicastIpAddressEntry(ref AddressRow row);
    public static void Apply(string ownedAdapterId)
    {
        if(!Guid.TryParse(ownedAdapterId,out var guid)||guid==Guid.Empty
            ||ConvertInterfaceGuidToLuid(ref guid,out var luid)!=0)throw new ClientFault("TUN_ADDRESS_FAILED");
        var bytes=IPAddress.Parse("fd73:6c78::1").GetAddressBytes();
        InitializeUnicastIpAddressEntry(out var row);
        row.Luid=luid;
        row.Address=new(){Family=23,First=BinaryPrimitives.ReadUInt64LittleEndian(bytes),Last=BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(8))};
        if(GetUnicastIpAddressEntry(ref row)!=0||row.Luid!=luid||row.PrefixLength!=126)
            throw new ClientFault("TUN_ADDRESS_FAILED");
        if(row.SkipAsSource!=0)return;
        // Preserve every native field returned by Get; change just source-address eligibility.
        row.SkipAsSource=1;
        if(SetUnicastIpAddressEntry(ref row)!=0)throw new ClientFault("TUN_ADDRESS_FAILED");
        if(GetUnicastIpAddressEntry(ref row)!=0||row.SkipAsSource!=1)throw new ClientFault("TUN_ADDRESS_FAILED");
    }
}
