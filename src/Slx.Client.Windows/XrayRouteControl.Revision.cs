using Slx.Client.Core;
namespace Slx.Client.Windows;
public sealed partial class XrayRouteControl
{
    internal static string ReadRevision(byte[] bytes)
    {
        var reader = new RouteProtoReader(bytes); var rules = new List<(string Outbound, string Marker)>();
        while (!reader.End)
        {
            var (field, wire) = reader.Key();
            if (field != 1) { reader.Skip(wire); continue; }
            if (wire != 2 || rules.Count >= 32) throw Invalid();
            var item = new RouteProtoReader(reader.Blob()); string outbound = "", marker = "";
            bool hadOutbound = false, hadMarker = false;
            while (!item.End)
            {
                var (entry, kind) = item.Key();
                if (entry is not (1 or 2)) { item.Skip(kind); continue; }
                if (kind != 2 || entry == 1 && hadOutbound || entry == 2 && hadMarker) throw Invalid();
                string value = item.Text(); if (value.Length > 128) throw Invalid();
                if (entry == 1) { outbound = value; hadOutbound = true; } else { marker = value; hadMarker = true; }
            }
            rules.Add((outbound, marker));
        }
        if (rules.Count < 2 || rules[0].Outbound != XrayRouteProtocol.ApiTag) throw Invalid();
        var markers = rules.Where(rule => rule.Marker.StartsWith(XrayRouteProtocol.MarkerPrefix, StringComparison.Ordinal)).ToArray();
        if (markers.Length != 1 || markers[0] != rules[^1] || markers[0].Outbound is not ("vpn" or "direct")) throw Invalid();
        string revision = markers[0].Marker[XrayRouteProtocol.MarkerPrefix.Length..];
        if (revision.Length != 64 || revision.Any(character => !char.IsAsciiHexDigitLower(character))) throw Invalid();
        return revision;
    }
    private static ClientFault Invalid() => new("ROUTING_INVALID_REPLY");
}
