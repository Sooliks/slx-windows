using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Slx.Client.Core;

namespace Slx.Client.Windows;

internal static class IntegratedUpdateStaging
{
    internal static async Task CopyAsync(string installation, Guid id, CancellationToken token)
    {
        string source = Path.Combine(installation, "updater"), destination = Path.Combine(IntegratedUpdatePaths.Session(id), "host");
        string manifest = Path.Combine(source, "updater-manifest.json"); IntegratedUpdatePaths.File(manifest);
        using var input = new FileStream(manifest, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (input.Length is <= 0 or > 262144) throw new ClientFault("UPDATE_INVALID");
        var options = new JsonSerializerOptions { MaxDepth = 8, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
        var bundle = await JsonSerializer.DeserializeAsync<IntegratedUpdateBundle>(input, options, token)
            ?? throw new ClientFault("UPDATE_INVALID");
        Validate(bundle);
        Directory.CreateDirectory(destination); IntegratedUpdatePaths.NoReparse(destination);
        foreach (var item in bundle.Files)
        {
            token.ThrowIfCancellationRequested();
            string from = Path.Combine(source, item.Name), to = Path.Combine(destination, item.Name);
            IntegratedUpdatePaths.File(from);
            Directory.CreateDirectory(Path.GetDirectoryName(to)!); IntegratedUpdatePaths.NoReparse(Path.GetDirectoryName(to)!);
            using var file = new FileStream(from, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous);
            if (file.Length != item.Size || !CryptographicOperations.FixedTimeEquals(await SHA256.HashDataAsync(file, token), Convert.FromHexString(item.Sha256)))
                throw new ClientFault("UPDATE_INTEGRITY_FAILED");
            file.Position = 0;
            using var output = new FileStream(to, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.Asynchronous);
            await file.CopyToAsync(output, token); await output.FlushAsync(token);
        }
    }
    internal static void Validate(IntegratedUpdateBundle bundle)
    {
        if (bundle.Files is null || bundle.Files.Count is <= 0 or > 1024 || bundle.Files.Any(file => file is null))
            throw new ClientFault("UPDATE_INVALID");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long total = 0;
        foreach (var file in bundle.Files)
        {
            var pieces = file.Name?.Split(['/', '\\']);
            if (pieces is null || pieces.Length is <= 0 or > 3 || pieces.Any(part => part.Length == 0 || part is "." or ".."
                || part.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('.' or '_' or '-' or ' ')))
                || !names.Add(file.Name!) || file.Size is <= 0 or > 150L * 1024 * 1024 || !IntegratedUpdatePolicy.Fingerprint(file.Sha256))
                throw new ClientFault("UPDATE_INVALID");
            total += file.Size;
            if (total > 512L * 1024 * 1024) throw new ClientFault("UPDATE_INVALID");
        }
        if (!names.Contains(IntegratedUpdatePaths.HostName)) throw new ClientFault("UPDATE_INVALID");
    }
}
