using System.Text.Json;
using System.Text.RegularExpressions;

namespace Slx.Client.Core;

public static partial class UpdateReleaseCatalog
{
    public static ReleaseUpdateMetadata? Latest(string json, string installedVersion)
    {
        if (json.Length > 2097152 || !ReleaseVersion.TryParse(installedVersion, out var current)) throw new ClientFault("UPDATE_INVALID");
        try
        {
            using var document = JsonDocument.Parse(json, new() { MaxDepth = 16 });
            if (document.RootElement.ValueKind != JsonValueKind.Array || document.RootElement.GetArrayLength() > 20)
                throw new ClientFault("UPDATE_INVALID");
            ReleaseUpdateMetadata? latest = null;
            var latestVersion = current;
            foreach (var release in document.RootElement.EnumerateArray())
            {
                if (release.ValueKind != JsonValueKind.Object || Boolean(release, "draft") != false) continue;
                var tag = Text(release, "tag_name");
                if (tag is null || !tag.StartsWith('v') || !ReleaseVersion.TryParse(tag[1..], out var version)
                    || Boolean(release, "prerelease") != version.IsPrerelease || (!current.IsPrerelease && version.IsPrerelease)
                    || version.CompareTo(latestVersion) <= 0) continue;
                var metadata = ReadRelease(release, version.Value);
                if (metadata is null) continue;
                latest = metadata; latestVersion = version;
            }
            return latest;
        }
        catch (JsonException) { throw new ClientFault("UPDATE_INVALID"); }
    }
    public static string Checksum(string body, string installerName)
    {
        if (body.Length is <= 0 or > 65536) throw new ClientFault("UPDATE_INVALID");
        var seen = new HashSet<string>(StringComparer.Ordinal); string? checksum = null;
        foreach (var line in body.TrimEnd('\r', '\n').Split('\n'))
        {
            var match = ChecksumLine().Match(line.TrimEnd('\r'));
            if (!match.Success || !seen.Add(match.Groups[2].Value)) throw new ClientFault("UPDATE_INVALID");
            if (match.Groups[2].Value == installerName) checksum = match.Groups[1].Value.ToLowerInvariant();
        }
        return checksum ?? throw new ClientFault("UPDATE_INVALID");
    }
    private static ReleaseUpdateMetadata? ReadRelease(JsonElement release, string version)
    {
        if (!Uri.TryCreate(Text(release, "html_url"), UriKind.Absolute, out var page) || !UpdateSourcePolicy.Secure(page)
            || page != UpdateSourcePolicy.ReleasePage(version)
            || !release.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array || assets.GetArrayLength() > 32) return null;
        var name = UpdateSourcePolicy.InstallerName(version);
        var installer = ReadAsset(assets, version, name, UpdateSourcePolicy.MaxInstallerBytes);
        var checksum = ReadAsset(assets, version, "SHA256SUMS.txt", 65536);
        return installer is null || checksum is null ? null : new(new(version, page, name, installer.Value.Url, installer.Value.Bytes, ""),
            checksum.Value.Url, checksum.Value.Bytes);
    }
    private static (Uri Url, long Bytes)? ReadAsset(JsonElement assets, string version, string name, long maximum)
    {
        (Uri Url, long Bytes)? found = null;
        foreach (var asset in assets.EnumerateArray())
        {
            if (asset.ValueKind != JsonValueKind.Object || Text(asset, "name") != name) continue;
            if (found is not null || Text(asset, "state") != "uploaded" || !asset.TryGetProperty("size", out var size)
                || size.ValueKind != JsonValueKind.Number || !size.TryGetInt64(out var bytes) || bytes <= 0 || bytes > maximum
                || !Uri.TryCreate(Text(asset, "browser_download_url"), UriKind.Absolute, out var url)
                || !UpdateSourcePolicy.AssetUrl(url, version, name)) return null;
            found = (url, bytes);
        }
        return found;
    }
    private static string? Text(JsonElement owner, string name) => owner.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static bool? Boolean(JsonElement owner, string name) => owner.TryGetProperty(name, out var value)
        && value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() : null;
    [GeneratedRegex(@"\A([0-9a-fA-F]{64}) {2}([A-Za-z0-9.+_-]+)\z", RegexOptions.CultureInvariant)]
    private static partial Regex ChecksumLine();
}
