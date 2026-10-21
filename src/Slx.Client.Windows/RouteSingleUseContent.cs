using System.Net;
using System.Net.Http.Headers;
using Slx.Client.Core;
namespace Slx.Client.Windows;
// HttpClient may internally retry HTTP/2. A mutation body can be serialized only once.
internal sealed class RouteSingleUseContent(byte[] bytes) : HttpContent
{
    private int sent;
    public static RouteSingleUseContent Create(byte[] payload)
    {
        var content = new RouteSingleUseContent(RouteGrpcMessage.Frame(payload));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/grpc+proto"); return content;
    }
    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => WriteAsync(stream, CancellationToken.None);
    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken) => WriteAsync(stream, cancellationToken);
    private async Task WriteAsync(Stream stream, CancellationToken token)
    {
        if (Interlocked.Exchange(ref sent, 1) != 0) throw new ClientFault("ROUTING_UNAVAILABLE");
        await stream.WriteAsync(bytes, token).ConfigureAwait(false);
    }
    protected override bool TryComputeLength(out long length) { length = bytes.Length; return true; }
}
