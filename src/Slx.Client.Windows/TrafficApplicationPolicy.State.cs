using Slx.Client.Core;
namespace Slx.Client.Windows;
internal sealed record TrafficApplicationFilter(byte[] Identity,Guid V4,Guid V6)
{
    public override string ToString()=>"TrafficApplicationFilter [redacted]";
}
internal sealed record TrafficApplicationUpdate(IReadOnlyList<TrafficApplicationFilter> Added,IReadOnlyList<TrafficApplicationFilter> Removed);
/// <summary>Committed identity-to-filter ownership. A failed native commit invalidates the baseline.</summary>
internal sealed class TrafficApplicationState
{
    private TrafficApplicationPolicy current;
    private Dictionary<string,TrafficApplicationFilter> filters;
    private bool valid=true;
    public TrafficApplicationPolicy Current=>valid?current:throw new ClientFault("TRAFFIC_PROTECTION_APPLICATION_BASELINE");
    public IReadOnlyDictionary<string,TrafficApplicationFilter> Filters=>filters;
    public TrafficApplicationState(TrafficApplicationPolicy policy)
    {
        current=policy;
        filters=policy.Entries.ToDictionary(entry=>entry.Key,entry=>NewFilter(entry.Value),StringComparer.Ordinal);
    }
    public void Invalidate()=>valid=false;
    public void Apply(TrafficApplicationPolicy target,Action<TrafficApplicationUpdate> commit)
    {
        _ = Current;
        var next=new Dictionary<string,TrafficApplicationFilter>(StringComparer.Ordinal);
        var added=new List<TrafficApplicationFilter>();
        foreach(var entry in target.Entries)
        {
            if(!filters.TryGetValue(entry.Key,out var filter)){filter=NewFilter(entry.Value);added.Add(filter);}
            next.Add(entry.Key,filter);
        }
        var removed=filters.Where(entry=>!next.ContainsKey(entry.Key)).Select(entry=>entry.Value).ToArray();
        if(added.Count==0&&removed.Length==0)return;
        try{commit(new(added,removed));}
        catch{Invalidate();throw;}
        filters=next;current=target;
    }
    private static TrafficApplicationFilter NewFilter(byte[] identity)=>new(identity,Guid.NewGuid(),Guid.NewGuid());
}
