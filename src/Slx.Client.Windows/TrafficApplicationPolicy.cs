namespace Slx.Client.Windows;
/// <summary>Opaque native app identities; paths never need to be re-opened during union/rollback.</summary>
public sealed class TrafficApplicationPolicy
{
    internal IReadOnlyDictionary<string,byte[]> Entries{get;}
    public int Count=>Entries.Count;
    private TrafficApplicationPolicy(IReadOnlyDictionary<string,byte[]> entries)=>Entries=entries;
    internal static TrafficApplicationPolicy FromIdentities(IEnumerable<byte[]> identities)
    {
        var entries=new Dictionary<string,byte[]>(StringComparer.Ordinal);
        foreach(var identity in identities)
        {
            if(identity.Length is 0 or >65536)throw new Slx.Client.Core.ClientFault("TRAFFIC_PROTECTION_APPLICATION_INVALID");
            entries.TryAdd(Convert.ToHexString(identity),identity.ToArray());
            if(entries.Count>256)throw new Slx.Client.Core.ClientFault("TRAFFIC_PROTECTION_APPLICATION_LIMIT");
        }
        return new(entries);
    }
    public TrafficApplicationPolicy Union(TrafficApplicationPolicy other)=>FromIdentities(Entries.Values.Concat(other.Entries.Values));
    public override string ToString()=>"TrafficApplicationPolicy [redacted]";
}
