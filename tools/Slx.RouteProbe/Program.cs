using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

if (args.Length != 1 || args[0] is not ("selected" or "other")) return 2;
using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(8));
byte[] payload = Encoding.ASCII.GetBytes("SLX-TUN-ACCEPTANCE");
try
{
    using var tcp = new TcpClient(AddressFamily.InterNetwork);
    await tcp.ConnectAsync(IPAddress.Parse("198.18.0.2"), 45467, budget.Token);
    Console.Error.WriteLine("TCP_CONNECTED");
    var stream = tcp.GetStream();
    await stream.WriteAsync(payload, budget.Token);
    byte[] answer = new byte[32];
    int count = await stream.ReadAsync(answer, budget.Token);
    string tcpRole = Encoding.ASCII.GetString(answer, 0, count);
    Console.Error.WriteLine("TCP_RESPONSE=" + tcpRole);
    using var udp = new UdpClient(AddressFamily.InterNetwork);
    udp.Connect(IPAddress.Parse("198.18.0.2"), 45467);
    await udp.SendAsync(payload, budget.Token);
    Console.Error.WriteLine("UDP_SENT");
    var datagram = await udp.ReceiveAsync(budget.Token);
    string udpRole = Encoding.ASCII.GetString(datagram.Buffer);
    Console.Error.WriteLine("UDP_RESPONSE=" + udpRole);
    bool passed = tcpRole == args[0] && udpRole == args[0];
    Console.WriteLine(JsonSerializer.Serialize(new { passed, tcp = tcpRole, udp = udpRole }));
    return passed ? 0 : 1;
}
catch (Exception error) when (error is SocketException or IOException or OperationCanceledException)
{
    Console.WriteLine("{\"passed\":false,\"code\":\"ROUTING_PROBE_FAILED\"}");
    return 1;
}
