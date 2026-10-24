using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Slx.Client.Core;
using Slx.Client.Windows;

namespace Slx.Client.Tests;

internal sealed class UpdateFixtures : IDisposable
{
    internal const string Version = "0.1.0-alpha.11";
    internal static readonly byte[] Installer = Encoding.UTF8.GetBytes("SLX update fixture; never an executable.");
    internal static string Digest => Convert.ToHexStringLower(SHA256.HashData(Installer));
    internal string Root { get; } = Path.Combine(Path.GetTempPath(), "slx-update-tests-" + Guid.NewGuid().ToString("N"));
    internal static string Checksums(string version = Version) => Digest + "  " + UpdateSourcePolicy.InstallerName(version) + "\n";
    internal static UpdateOffer Offer(string version = Version) => new(version, UpdateSourcePolicy.ReleasePage(version),
        UpdateSourcePolicy.InstallerName(version), AssetUrl(version, UpdateSourcePolicy.InstallerName(version)), Installer.Length, Digest);
    internal static Uri AssetUrl(string version, string name) => new(UpdateSourcePolicy.Repository + "/releases/download/v" + version + "/" + name);
    internal static Dictionary<string, object?> Release(string version = Version)
    {
        var name = UpdateSourcePolicy.InstallerName(version);
        return new() { ["tag_name"] = "v" + version, ["draft"] = false, ["prerelease"] = version.Contains('-'),
            ["html_url"] = UpdateSourcePolicy.ReleasePage(version).AbsoluteUri,
            ["assets"] = new[] { Asset(version, name, Installer.Length), Asset(version, "SHA256SUMS.txt", Encoding.UTF8.GetByteCount(Checksums(version))) } };
    }
    internal static Dictionary<string, object?> Asset(string version, string name, long size) => new()
    { ["name"] = name, ["size"] = size, ["state"] = "uploaded", ["browser_download_url"] = AssetUrl(version, name).AbsoluteUri };
    internal static string Feed(params object[] releases) => JsonSerializer.Serialize(releases);
    internal ReleaseUpdateClient Client(HttpMessageHandler handler, Func<ProcessStartInfo, bool>? launch = null, TimeSpan? timeout = null) =>
        // Success fixtures exercise real ACL/ADS disk I/O, not a two-second performance contract.
        // Timeout cases explicitly inject 100ms; fixture deadlines are independent of production policy.
        new(() => handler, Root, launch ?? (_ => throw new InvalidOperationException("Unexpected launch")), timeout ?? TimeSpan.FromSeconds(15), timeout ?? TimeSpan.FromSeconds(15));
    internal static HttpResponseMessage Response(byte[] content) => new(HttpStatusCode.OK) { Content = new ByteArrayContent(content) };
    internal static HttpResponseMessage Text(string content) => Response(Encoding.UTF8.GetBytes(content));
    public void Dispose()
    {
        var prefix = Path.Combine(Path.GetTempPath(), "slx-update-tests-");
        if (Root.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && Directory.Exists(Root)) Directory.Delete(Root, true);
    }
    internal sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        internal int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        { Calls++; return send(request, cancellationToken); }
    }
    internal sealed class StalledStream : MemoryStream
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        { await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken); return 0; }
    }
    internal sealed class UnknownLengthStream(byte[] bytes) : MemoryStream(bytes) { public override bool CanSeek => false; }
    internal sealed class Progress : IProgress<int>
    {
        internal List<int> Values { get; } = [];
        public void Report(int value) => Values.Add(value);
    }
}
