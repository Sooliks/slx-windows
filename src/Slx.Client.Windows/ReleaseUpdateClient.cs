using System.Diagnostics;
using System.Net.Http;
using System.Text;
using Slx.Client.Core;

namespace Slx.Client.Windows;

/// <summary>Public same-repository HTTPS releases are trusted; SHA-256 verifies transport integrity, not publisher signing.</summary>
public sealed partial class ReleaseUpdateClient
{
    private readonly Func<HttpMessageHandler> createHandler;
    private readonly Func<ProcessStartInfo, bool> launch;
    private readonly string root;
    private readonly TimeSpan checkTimeout;
    private readonly TimeSpan downloadTimeout;
    public ReleaseUpdateClient() : this(() => new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false, UseCookies = false },
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SLX", "VPN", "Updates"),
        start => { using var process = Process.Start(start); return process is not null; }, TimeSpan.FromSeconds(15), TimeSpan.FromMinutes(5)) { }
    internal ReleaseUpdateClient(Func<HttpMessageHandler> handlerFactory, string directory, Func<ProcessStartInfo, bool> start,
        TimeSpan checkDeadline, TimeSpan downloadDeadline)
    {
        createHandler = handlerFactory; root = Path.GetFullPath(directory); launch = start;
        checkTimeout = checkDeadline; downloadTimeout = downloadDeadline;
    }
    public async Task<UpdateCheckResult> CheckAsync(string installedVersion, CancellationToken cancellationToken = default)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(checkTimeout);
        try
        {
            using var transport = new UpdateTransport(createHandler());
            var payload = await transport.ReadAsync(new(UpdateSourcePolicy.Feed), 2097152, null, false, budget.Token);
            var release = UpdateReleaseCatalog.Latest(Utf8(payload), installedVersion);
            if (release is null) return new(UpdateCheckState.Current);
            var checksums = await transport.ReadAsync(release.ChecksumUrl, 65536, release.ChecksumBytes, true, budget.Token);
            var offer = release.Installer with { Sha256 = UpdateReleaseCatalog.Checksum(Utf8(checksums), release.Installer.AssetName) };
            UpdateSourcePolicy.Validate(offer);
            return new(UpdateCheckState.Available, offer);
        }
        catch (ClientFault fault) { return new(UpdateCheckState.Unavailable, ErrorCode: fault.Code); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { return new(UpdateCheckState.Unavailable, ErrorCode: "UPDATE_UNAVAILABLE"); }
        catch (Exception exception) when (exception is HttpRequestException or IOException or DecoderFallbackException)
        { return new(UpdateCheckState.Unavailable, ErrorCode: "UPDATE_UNAVAILABLE"); }
    }
    private static string Utf8(byte[] bytes) => new UTF8Encoding(false, true).GetString(bytes);
}
