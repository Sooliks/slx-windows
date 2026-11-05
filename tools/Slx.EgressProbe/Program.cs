using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;

// Acceptance-only probe. Built twice under different names: one copy is selected in SLX, the other is not.
// "ip" reports the public address seen over TCP (HTTPS) and UDP (STUN); "hold" keeps requesting for N seconds.
if (args.Length < 1 || args[0] is not ("ip" or "hold")) return 2;
if (args[0] == "ip")
{
    string? tcp = await TcpAddressAsync(), udp = await UdpAddressAsync();
    Console.WriteLine(JsonSerializer.Serialize(new { tcp, udp }));
    return tcp is null && udp is null ? 1 : 0;
}
int seconds = args.Length > 1 && int.TryParse(args[1], out var value) && value is > 0 and <= 600 ? value : 60;
using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(4) };
int ok = 0, failed = 0, longestGapMs = 0;
var clock = Stopwatch.StartNew(); long lastOk = 0;
while (clock.Elapsed < TimeSpan.FromSeconds(seconds))
{
    try
    {
        using var response = await client.GetAsync("https://www.gstatic.com/generate_204");
        if (response.StatusCode != HttpStatusCode.NoContent) throw new HttpRequestException();
        longestGapMs = Math.Max(longestGapMs, (int)(clock.ElapsedMilliseconds - lastOk));
        lastOk = clock.ElapsedMilliseconds; ok++;
    }
    catch (Exception error) when (error is HttpRequestException or TaskCanceledException) { failed++; }
    await Task.Delay(500);
}
longestGapMs = Math.Max(longestGapMs, (int)(clock.ElapsedMilliseconds - lastOk));
Console.WriteLine(JsonSerializer.Serialize(new { ok, failed, longestGapMs }));
return 0;

static async Task<string?> TcpAddressAsync()
{
    try
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        string text = (await client.GetStringAsync("https://api.ipify.org")).Trim();
        return IPAddress.TryParse(text, out var address) ? address.ToString() : null;
    }
    catch (Exception error) when (error is HttpRequestException or TaskCanceledException) { return null; }
}

static async Task<string?> UdpAddressAsync()
{
    foreach (var (host, port) in new[] { ("stun.l.google.com", 19302), ("stun.cloudflare.com", 3478) })
    {
        try
        {
            using var udp = new UdpClient(AddressFamily.InterNetwork);
            var target = (await Dns.GetHostAddressesAsync(host)).First(item => item.AddressFamily == AddressFamily.InterNetwork);
            byte[] request = new byte[20];
            request[1] = 1; request[4] = 0x21; request[5] = 0x12; request[6] = 0xA4; request[7] = 0x42;
            RandomNumberGenerator.Fill(request.AsSpan(8, 12));
            await udp.SendAsync(request, new IPEndPoint(target, port));
            using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(4));
            var reply = (await udp.ReceiveAsync(budget.Token)).Buffer;
            for (int offset = 20; offset + 12 <= reply.Length;)
            {
                int type = reply[offset] << 8 | reply[offset + 1], length = reply[offset + 2] << 8 | reply[offset + 3];
                if (type == 0x0020 && length >= 8 && reply[offset + 5] == 1)
                    return new IPAddress(new[] { (byte)(reply[offset + 8] ^ 0x21), (byte)(reply[offset + 9] ^ 0x12),
                        (byte)(reply[offset + 10] ^ 0xA4), (byte)(reply[offset + 11] ^ 0x42) }).ToString();
                offset += 4 + (length + 3) / 4 * 4;
            }
        }
        catch (Exception error) when (error is SocketException or OperationCanceledException or InvalidOperationException) { }
    }
    return null;
}
