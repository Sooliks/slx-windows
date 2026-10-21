using System.Text;
using Slx.Client.Core;
namespace Slx.Client.Windows;
internal sealed class RouteProtoReader(byte[] bytes)
{
    private int offset;
    public bool End => offset == bytes.Length;
    public ulong Number() => Varint();
    public (int Field, int Wire) Key()
    {
        ulong key = Varint();
        if (key >> 3 is 0 or > 536870911) throw Invalid();
        return ((int)(key >> 3), (int)(key & 7));
    }
    public byte[] Blob()
    {
        ulong length = Varint();
        if (length > (ulong)(bytes.Length - offset)) throw Invalid();
        var result = bytes.AsSpan(offset, (int)length).ToArray(); offset += (int)length; return result;
    }
    public string Text()
    {
        try { return new UTF8Encoding(false, true).GetString(Blob()); }
        catch (DecoderFallbackException) { throw Invalid(); }
    }
    public void Skip(int wire)
    {
        if (wire == 0) { Varint(); return; }
        if (wire == 2) { Blob(); return; }
        int size = wire == 1 ? 8 : wire == 5 ? 4 : throw Invalid();
        if (size > bytes.Length - offset) throw Invalid(); offset += size;
    }
    private ulong Varint()
    {
        ulong value = 0;
        for (int index = 0; index < 10; index++)
        {
            if (offset >= bytes.Length) throw Invalid();
            byte item = bytes[offset++];
            if (index == 9 && item > 1) throw Invalid();
            value |= (ulong)(item & 127) << (index * 7);
            if ((item & 128) == 0) return value;
        }
        throw Invalid();
    }
    private static ClientFault Invalid() => new("ROUTING_INVALID_REPLY");
}
