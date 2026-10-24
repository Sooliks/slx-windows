using System.Buffers.Binary;
using System.IO.Compression;
using Slx.Client.Core;

namespace Slx.Client.Windows;

internal static class UpdateCatalogCompression
{
    private static readonly uint[] CrcTable = CreateCrcTable();
    internal static async Task<byte[]> DecodeAsync(byte[] encoded, long maximum, CancellationToken cancellationToken)
    {
        if (encoded.Length < 18 || encoded.Length > maximum) throw new ClientFault("UPDATE_INVALID");
        try
        {
            using var source = new MemoryStream(encoded, false);
            using var gzip = new GZipStream(source, CompressionMode.Decompress);
            using var output = new MemoryStream();
            var buffer = new byte[65536]; long received = 0; int count;
            while ((count = await gzip.ReadAsync(buffer, cancellationToken)) > 0)
            {
                received += count;
                if (received > maximum) throw new ClientFault("UPDATE_INVALID");
                await output.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
            }
            var decoded = output.ToArray();
            // GZipStream can return complete JSON even when a gzip trailer is truncated.
            // Require the final CRC/size to agree with the entire bounded catalog body.
            if (decoded.Length == 0 || BinaryPrimitives.ReadUInt32LittleEndian(encoded.AsSpan(encoded.Length - 4)) != decoded.Length ||
                BinaryPrimitives.ReadUInt32LittleEndian(encoded.AsSpan(encoded.Length - 8)) != Crc32(decoded, cancellationToken))
                throw new ClientFault("UPDATE_INVALID");
            cancellationToken.ThrowIfCancellationRequested();
            return decoded;
        }
        catch (InvalidDataException) { throw new ClientFault("UPDATE_INVALID"); }
    }
    private static uint Crc32(byte[] bytes, CancellationToken cancellationToken)
    {
        uint crc = uint.MaxValue;
        for (var index = 0; index < bytes.Length; index++)
        {
            if ((index & 4095) == 0) cancellationToken.ThrowIfCancellationRequested();
            crc = (crc >> 8) ^ CrcTable[(byte)(crc ^ bytes[index])];
        }
        return ~crc;
    }
    private static uint[] CreateCrcTable()
    {
        var table = new uint[256];
        for (var index = 0; index < table.Length; index++)
        {
            var value = (uint)index;
            for (var bit = 0; bit < 8; bit++) value = (value >> 1) ^ ((value & 1) == 0 ? 0u : 0xedb88320u);
            table[index] = value;
        }
        return table;
    }
}
