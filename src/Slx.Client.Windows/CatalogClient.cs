using System.Net.Http;
using Slx.Client.Core;
namespace Slx.Client.Windows;
public static class CatalogClient
{
    public static IReadOnlyList<CatalogApplication> Bundled()
    {
        using var stream=typeof(CatalogClient).Assembly.GetManifestResourceStream("Slx.Catalog.json")
            ??throw new ClientFault("INVALID_CATALOG");
        using var reader=new StreamReader(stream);
        return ApplicationCatalog.Parse(reader.ReadToEnd());
    }
    public static async Task<IReadOnlyList<CatalogApplication>> RefreshAsync(CancellationToken cancellationToken)
    {
        using var handler=new HttpClientHandler{UseProxy=false,AllowAutoRedirect=false,UseCookies=false};
        using var client=new HttpClient(handler){Timeout=TimeSpan.FromSeconds(4)};
        using var response=await client.GetAsync("https://slxvpn.com/api/desktop/catalog",HttpCompletionOption.ResponseHeadersRead,cancellationToken);
        if(!response.IsSuccessStatusCode||response.Content.Headers.ContentLength>65536)throw new ClientFault("CATALOG_UNAVAILABLE");
        using var stream=await response.Content.ReadAsStreamAsync(cancellationToken);
        using var buffer=new MemoryStream();var chunk=new byte[4096];int count;
        while((count=await stream.ReadAsync(chunk,cancellationToken))>0)
        {if(buffer.Length+count>65536)throw new ClientFault("INVALID_CATALOG");buffer.Write(chunk,0,count);}
        return ApplicationCatalog.Parse(System.Text.Encoding.UTF8.GetString(buffer.ToArray()));
    }
}
