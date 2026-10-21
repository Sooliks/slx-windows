using System.Buffers.Binary;
using System.Net;
using System.Net.Http.Headers;
using Slx.Client.Core;
namespace Slx.Client.Windows;
internal static class RouteGrpcMessage
{
    public static byte[] Frame(byte[] payload)
    {
        if (payload.Length > 256 * 1024) throw new ClientFault("INVALID_PLAN");
        var framed = new byte[payload.Length + 5]; BinaryPrimitives.WriteUInt32BigEndian(framed.AsSpan(1), (uint)payload.Length);
        payload.CopyTo(framed, 5); return framed;
    }
    public static async Task<byte[]> ReadAsync(HttpResponseMessage response, CancellationToken token)
    {
        if (response.Version.Major != 2 || response.StatusCode != HttpStatusCode.OK
            || response.Content.Headers.ContentType?.MediaType is not ("application/grpc" or "application/grpc+proto")) throw Invalid();
        await using var stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
        using var body = new MemoryStream(); var buffer = new byte[4096];
        int read;
        while ((read = await stream.ReadAsync(buffer, token).ConfigureAwait(false)) != 0)
        {
            if (body.Length + read > 64 * 1024) throw Invalid(); body.Write(buffer, 0, read);
        }
        var statuses = Values(response.Headers, "grpc-status").Concat(Values(response.TrailingHeaders, "grpc-status")).ToArray();
        if (statuses.Length != 1 || statuses[0] != "0") throw Invalid();
        var bytes = body.ToArray();
        if (bytes.Length < 5 || bytes[0] != 0 || BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(1)) != bytes.Length - 5) throw Invalid();
        return bytes[5..];
    }
    private static IEnumerable<string> Values(HttpHeaders headers, string name) => headers.TryGetValues(name, out var values) ? values : [];
    private static ClientFault Invalid() => new("ROUTING_INVALID_REPLY");
}
