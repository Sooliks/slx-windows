using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Slx.Client.Core;

namespace Slx.Client.Service;

internal static class DnsProbePacket
{
    internal static byte[] CreateQuery(ProbeTarget target)
    {
        var query = new List<byte>(new byte[] { 0, 0, 1, 0, 0, 1, 0, 0, 0, 0, 0, 0 });
        string host = target == ProbeTarget.Gstatic ? "www.gstatic.com" : "slxvpn.com";
        foreach (var label in host.Split('.'))
        { query.Add((byte)label.Length); query.AddRange(Encoding.ASCII.GetBytes(label)); }
        query.AddRange(new byte[] { 0, 0, 1, 0, 1 });
        var bytes = query.ToArray();
        RandomNumberGenerator.Fill(bytes.AsSpan(0, 2));
        return bytes;
    }

    internal static (DnsProbeOutcome Outcome, int? ResponseCode) Evaluate(byte[] query, byte[] response)
    {
        if (query.Length < 12 || response.Length is < 12 or > 4096
            || !response.AsSpan(0, 2).SequenceEqual(query.AsSpan(0, 2))
            || (response[2] & 0xf8) != 0x80 || (response[2] & 2) != 0 || (response[3] & 0x40) != 0
            || Number(response, 4) != 1) return (DnsProbeOutcome.InvalidResponse, null);
        int expectedOffset = 12, offset = 12;
        if (!Name(query, ref expectedOffset, out var expectedName)
            || !Name(response, ref offset, out var actualName) || expectedName != actualName
            || expectedOffset + 4 != query.Length || offset + 4 > response.Length
            || !response.AsSpan(offset, 4).SequenceEqual(query.AsSpan(expectedOffset, 4)))
            return (DnsProbeOutcome.InvalidResponse, null);
        offset += 4;
        int answers = Number(response, 6), records = answers + Number(response, 8) + Number(response, 10);
        if (records > 128) return (DnsProbeOutcome.InvalidResponse, null);
        bool address = false;
        for (int record = 0; record < records; record++)
        {
            if (!Name(response, ref offset, out var owner) || offset + 10 > response.Length)
                return (DnsProbeOutcome.InvalidResponse, null);
            int type = Number(response, offset), recordClass = Number(response, offset + 2);
            int length = Number(response, offset + 8);
            offset += 10;
            if (offset + length > response.Length || (type == 1 && recordClass == 1 && length != 4))
                return (DnsProbeOutcome.InvalidResponse, null);
            // The pinned Xray DNS outbound flattens A answers to the original question owner.
            if (record < answers && owner == expectedName && type == 1 && recordClass == 1 && length == 4) address = true;
            offset += length;
        }
        if (offset != response.Length) return (DnsProbeOutcome.InvalidResponse, null);
        int code = response[3] & 15;
        return (code == 0 && address ? DnsProbeOutcome.Success : DnsProbeOutcome.Rejected, code);
    }

    private static int Number(byte[] packet, int offset) => BinaryPrimitives.ReadUInt16BigEndian(packet.AsSpan(offset, 2));

    private static bool Name(byte[] packet, ref int offset, out string name)
    {
        name = "";
        int cursor = offset, next = -1, wireLength = 1;
        var result = new StringBuilder();
        for (int steps = 0; steps < 128 && cursor < packet.Length; steps++)
        {
            int size = packet[cursor++];
            if (size == 0)
            { offset = next < 0 ? cursor : next; name = result.ToString(); return true; }
            if ((size & 0xc0) == 0xc0)
            {
                if (cursor >= packet.Length) return false;
                int pointer = ((size & 63) << 8) | packet[cursor++];
                if (pointer < 12 || pointer >= cursor - 2) return false;
                if (next < 0) next = cursor;
                cursor = pointer;
                continue;
            }
            if (size > 63 || cursor + size > packet.Length || (wireLength += size + 1) > 255) return false;
            // Length prefixes retain label boundaries; only ASCII case is folded, never raw names exposed.
            result.Append(size).Append(':');
            for (int label = 0; label < size; label++)
            {
                byte value = packet[cursor++];
                result.Append((char)(value is >= (byte)'A' and <= (byte)'Z' ? value + 32 : value));
            }
            result.Append('.');
        }
        return false;
    }
}
