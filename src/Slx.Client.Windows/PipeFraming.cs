using System.Buffers.Binary;
using System.Text.Json;
using Slx.Client.Core;

namespace Slx.Client.Windows;

public static class PipeFraming
{
    private static readonly JsonSerializerOptions Json = new()
    { PropertyNameCaseInsensitive = false, MaxDepth = 16, UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow };

    public static async Task<T> ReadAsync<T>(Stream stream, CancellationToken cancellationToken = default)
    {
        byte[] header = new byte[4];
        await stream.ReadExactlyAsync(header, cancellationToken);
        int length = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (length is <= 0 or > PipeContract.MaxBytes) throw new ClientFault("IPC_PAYLOAD_LIMIT");
        byte[] payload = new byte[length];
        await stream.ReadExactlyAsync(payload, cancellationToken);
        try { return JsonSerializer.Deserialize<T>(payload, Json) ?? throw new ClientFault("IPC_INVALID"); }
        catch (JsonException) { throw new ClientFault("IPC_INVALID"); }
        finally { System.Security.Cryptography.CryptographicOperations.ZeroMemory(payload); }
    }

    public static async Task WriteAsync<T>(Stream stream, T value, CancellationToken cancellationToken = default)
    {
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(value, Json);
        try
        {
            if (payload.Length > PipeContract.MaxBytes) throw new ClientFault("IPC_PAYLOAD_LIMIT");
            byte[] header = new byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(header, payload.Length);
            await stream.WriteAsync(header, cancellationToken);
            await stream.WriteAsync(payload, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }
        finally { System.Security.Cryptography.CryptographicOperations.ZeroMemory(payload); }
    }
}
