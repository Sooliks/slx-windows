using System.Net;
using System.Net.Sockets;
using System.Text;
using Slx.Client.Core;
using Slx.Client.Windows;
namespace Slx.XrayRouteContract;
internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length != 1) { Console.Error.WriteLine("Pass the absolute pinned xray.exe path"); return 2; }
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            var before = new ConnectPlan(new("contract", "Local API contract", "hysteria2", "vpn.example.invalid", 8444,
                "synthetic-profile-secret", "vpn.example.invalid"), RoutingMode.SelectedApplications,
                [new(Guid.NewGuid(), "Synthetic application", @"C:\SLX Contract\before.exe")]);
            var after = before with { Applications = [before.Applications[0] with { ExecutablePath = @"C:\SLX Contract\after.exe" }] };
            using var core = new LocalCore(args[0], before);
            await core.ReadyAsync(XrayRouteRevision.For(before), budget.Token);
            await CheckAuthenticationAsync(core, before, budget.Token);
            using var echo = new TcpListener(IPAddress.Loopback, 0); echo.Start();
            int port = ((IPEndPoint)echo.LocalEndpoint).Port;
            var echoTask = EchoOnceAsync(echo, budget.Token);
            using var tunnel = await OpenAsync(core, "127.0.0.1:" + port, authenticated: true, budget.Token);
            await ExchangeAsync(tunnel.GetStream(), "before-policy-change", budget.Token);
            await core.Control.ReplaceAsync(after, budget.Token);
            Ensure(await core.Control.ReadRevisionAsync(budget.Token) == XrayRouteRevision.For(after), "New revision not observed");
            await ExchangeAsync(tunnel.GetStream(), "after-policy-change", budget.Token);
            // A second distinct policy confirms full replacement retained the internal API route.
            var whole = after with { Mode = RoutingMode.WholeComputer };
            await core.Control.ReplaceAsync(whole, budget.Token);
            Ensure(await core.Control.ReadRevisionAsync(budget.Token) == XrayRouteRevision.For(whole), "Whole-PC revision not observed");
            await ExchangeAsync(tunnel.GetStream(), "after-second-change", budget.Token);
            tunnel.Dispose(); await echoTask;
            Console.WriteLine("PASS: pinned Xray 26.9.9 authenticated internal API; full hot replacements, revision readback and existing TCP stream preserved.");
            Console.WriteLine("Scope: no TUN, WFP, installed service, real VPN or installed-system acceptance."); return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error is ClientFault fault ? "FAIL: " + fault.Code : "FAIL: " + error.GetType().Name);
            return 1;
        }
    }
    private static async Task CheckAuthenticationAsync(LocalCore core, ConnectPlan plan, CancellationToken token)
    {
        using var noAuth = new TcpClient(); await noAuth.ConnectAsync(IPAddress.Loopback, core.Port, token);
        await noAuth.GetStream().WriteAsync(Encoding.ASCII.GetBytes("CONNECT slx-control.invalid:80 HTTP/1.1\r\nHost: slx-control.invalid:80\r\n\r\n"), token);
        Ensure((await HeaderAsync(noAuth.GetStream(), token)).StartsWith("HTTP/1.1 407 ", StringComparison.Ordinal), "Unauthenticated CONNECT was accepted");
        bool rejected = false;
        try { await new XrayRouteControl(core.Port, core.User, new string('0', 64)).ReplaceAsync(plan, token); }
        catch (ClientFault) { rejected = true; }
        Ensure(rejected, "Wrong credentials reached RoutingService");
        Ensure(await core.Control.ReadRevisionAsync(token) == XrayRouteRevision.For(plan), "Rejected request changed revision");
    }
    private static async Task<TcpClient> OpenAsync(LocalCore core, string destination, bool authenticated, CancellationToken token)
    {
        var client = new TcpClient();
        try
        {
            await client.ConnectAsync(IPAddress.Loopback, core.Port, token);
            string auth = authenticated ? "Proxy-Authorization: Basic " + Convert.ToBase64String(Encoding.ASCII.GetBytes(core.User + ":" + core.Password)) + "\r\n" : "";
            byte[] request = Encoding.ASCII.GetBytes("CONNECT " + destination + " HTTP/1.1\r\nHost: " + destination + "\r\n" + auth + "\r\n");
            await client.GetStream().WriteAsync(request, token);
            Ensure((await HeaderAsync(client.GetStream(), token)).StartsWith("HTTP/1.1 200 ", StringComparison.Ordinal), "Echo CONNECT failed"); return client;
        }
        catch { client.Dispose(); throw; }
    }
    private static async Task<string> HeaderAsync(Stream stream, CancellationToken token)
    {
        var bytes = new List<byte>(); var one = new byte[1];
        while (bytes.Count < 4096)
        {
            Ensure(await stream.ReadAsync(one, token) == 1, "Truncated proxy reply"); bytes.Add(one[0]);
            if (bytes.Count >= 4 && bytes.TakeLast(4).SequenceEqual(new byte[] { 13, 10, 13, 10 })) return Encoding.ASCII.GetString(bytes.ToArray());
        }
        throw new InvalidOperationException("Oversize proxy reply");
    }
    private static async Task EchoOnceAsync(TcpListener listener, CancellationToken token)
    {
        using var client = await listener.AcceptTcpClientAsync(token); var stream = client.GetStream(); var buffer = new byte[256]; int read;
        while ((read = await stream.ReadAsync(buffer, token)) != 0) await stream.WriteAsync(buffer.AsMemory(0, read), token);
    }
    private static async Task ExchangeAsync(Stream stream, string text, CancellationToken token)
    {
        byte[] sent = Encoding.ASCII.GetBytes(text); await stream.WriteAsync(sent, token);
        var reply = new byte[sent.Length]; await stream.ReadExactlyAsync(reply, token);
        Ensure(sent.SequenceEqual(reply), "Established TCP stream changed or closed");
    }
    private static void Ensure(bool success, string reason) { if (!success) throw new InvalidOperationException(reason); }
}
