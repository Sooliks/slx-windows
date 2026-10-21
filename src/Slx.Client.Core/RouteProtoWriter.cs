using System.Text;
namespace Slx.Client.Core;
internal sealed class RouteProtoWriter
{
    private readonly MemoryStream output = new();
    public RouteProtoWriter Number(int field, ulong value) { Varint((ulong)(field << 3)); Varint(value); return this; }
    public RouteProtoWriter Text(int field, string value) => Bytes(field, Encoding.UTF8.GetBytes(value));
    public RouteProtoWriter Bytes(int field, byte[] bytes)
    {
        Varint((ulong)(field << 3 | 2)); Varint((ulong)bytes.Length); output.Write(bytes); return this;
    }
    public byte[] Finish() => output.ToArray();
    private void Varint(ulong value)
    {
        while (value > 127) { output.WriteByte((byte)(value | 128)); value >>= 7; }
        output.WriteByte((byte)value);
    }
}
