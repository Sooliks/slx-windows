using System.Text.Json;
using System.Text.RegularExpressions;
namespace Slx.Client.Core;
public sealed record CatalogApplication(string Id,string Name,string Category,IReadOnlyList<string> Executables);
public static partial class ApplicationCatalog
{
    public static IReadOnlyList<CatalogApplication> Parse(string json)
    {
        if(json.Length>65536)throw new ClientFault("INVALID_CATALOG");
        try
        {
            using var document=JsonDocument.Parse(json);
            var root=document.RootElement;
            if(root.GetProperty("schemaVersion").GetInt32()!=1)throw new ClientFault("INVALID_CATALOG");
            var rows=root.GetProperty("applications");
            if(rows.GetArrayLength()>80)throw new ClientFault("INVALID_CATALOG");
            var found=new List<CatalogApplication>();var ids=new HashSet<string>(StringComparer.Ordinal);
            foreach(var row in rows.EnumerateArray())
            {
                var id=row.GetProperty("id").GetString()??"";var name=row.GetProperty("name").GetString()??"";
                var category=row.GetProperty("category").GetString()??"";
                var executables=row.GetProperty("executables").EnumerateArray().Select(x=>x.GetString()??"").ToArray();
                if(!Id().IsMatch(id)||!ids.Add(id)||string.IsNullOrWhiteSpace(name)||name.Length>80||name.Any(char.IsControl)
                    ||!Id().IsMatch(category)||executables.Length is <1 or >5||executables.Any(x=>!Executable().IsMatch(x)))
                    throw new ClientFault("INVALID_CATALOG");
                found.Add(new(id,name,category,executables));
            }
            return found;
        }
        catch(Exception error) when(error is JsonException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException)
        {throw new ClientFault("INVALID_CATALOG");}
    }
    [GeneratedRegex(@"\A[a-z][a-z0-9-]{0,39}\z")]private static partial Regex Id();
    [GeneratedRegex(@"\A[A-Za-z0-9][A-Za-z0-9 ._-]{0,74}\.exe\z",RegexOptions.IgnoreCase)]private static partial Regex Executable();
}
