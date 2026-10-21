using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using Slx.Client.Core;
using Slx.Client.Windows;
namespace Slx.XrayRouteContract;
internal sealed class LocalCore : IDisposable
{
    private const string ExpectedHash = "0d0fc0ea2b05641acb78c01fc36ad694e7b029861b2d5eb93da0e3e9fda9a98f";
    private readonly Process child;
    public int Port { get; }
    public string User { get; } = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
    public string Password { get; } = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    public XrayRouteControl Control => new(Port, User, Password);
    public LocalCore(string executable, ConnectPlan plan)
    {
        if (!Path.IsPathFullyQualified(executable)) throw new InvalidOperationException("Absolute pinned runtime path required");
        using (var input = File.OpenRead(executable))
            if (Convert.ToHexStringLower(SHA256.HashData(input)) != ExpectedHash) throw new InvalidOperationException("Pinned core checksum mismatch");
        using (var reservation = new TcpListener(IPAddress.Loopback, 0))
        { reservation.Start(); Port = ((IPEndPoint)reservation.LocalEndpoint).Port; }
        using var original = JsonDocument.Parse(XrayConfigBuilder.Build(plan, Port, User, Password));
        var root = original.RootElement;
        // No TUN, service, WFP, external proxy or production profile: only authenticated localhost and loopback echo.
        var config = JsonSerializer.Serialize(new
        {
            api = root.GetProperty("api"), log = root.GetProperty("log"),
            inbounds = new[] { root.GetProperty("inbounds")[1] },
            outbounds = new[] { Freedom("direct"), Freedom("vpn"), Freedom("dns") },
            routing = root.GetProperty("routing")
        });
        var info = new ProcessStartInfo(executable)
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true,
            RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = Path.GetDirectoryName(executable)!
        };
        foreach (var argument in new[] { "run", "-config", "stdin:", "-format", "json" }) info.ArgumentList.Add(argument);
        info.Environment.Clear(); info.Environment["PATH"] = Environment.SystemDirectory;
        info.Environment["SYSTEMROOT"] = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        child = new Process { StartInfo = info };
        if (!child.Start()) throw new InvalidOperationException("Pinned core did not start");
        _ = DrainAsync(child.StandardOutput.BaseStream); _ = DrainAsync(child.StandardError.BaseStream);
        child.StandardInput.Write(config); child.StandardInput.Close();
    }
    public async Task ReadyAsync(string revision, CancellationToken token)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(token); budget.CancelAfter(TimeSpan.FromSeconds(15));
        while (!budget.IsCancellationRequested)
        {
            if (child.HasExited) throw new InvalidOperationException("Pinned core exited before API readiness");
            try { if (await Control.ReadRevisionAsync(budget.Token) == revision) return; }
            catch (ClientFault) { }
            await Task.Delay(100, budget.Token);
        }
        throw new InvalidOperationException("Pinned API readiness failed");
    }
    private static object Freedom(string tag) => new { tag, protocol = "freedom", settings = new { domainStrategy = "AsIs" } };
    private static async Task DrainAsync(Stream stream)
    {
        var buffer = new byte[4096];
        try { while (await stream.ReadAsync(buffer) != 0) { } } catch (IOException) { }
    }
    public void Dispose()
    {
        if (!child.HasExited) { child.Kill(entireProcessTree: true); child.WaitForExit(5000); }
        child.Dispose();
    }
}
