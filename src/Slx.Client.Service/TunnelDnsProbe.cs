using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using Slx.Client.Core;
namespace Slx.Client.Service;
internal static class TunnelDnsProbe
{
    public static async Task CheckAsync(CancellationToken cancellationToken)
    {
        using var budget=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);budget.CancelAfter(TimeSpan.FromSeconds(9));
        try
        {
            for(int attempt=0;attempt<3;attempt++)
            {
                using var udp=new UdpClient(AddressFamily.InterNetwork);
                var query=new List<byte>(new byte[]{0,0,1,0,0,1,0,0,0,0,0,0});
                string host=attempt%2==0?"www.gstatic.com":"slxvpn.com";
                foreach(var label in host.Split('.')){query.Add((byte)label.Length);query.AddRange(System.Text.Encoding.ASCII.GetBytes(label));}
                query.AddRange(new byte[]{0,0,1,0,1});var bytes=query.ToArray();RandomNumberGenerator.Fill(bytes.AsSpan(0,2));
                using var deadline=CancellationTokenSource.CreateLinkedTokenSource(budget.Token);deadline.CancelAfter(TimeSpan.FromSeconds(3));
                try
                {
                    udp.Connect(IPAddress.Parse("1.1.1.1"),53);await udp.SendAsync(bytes,deadline.Token);
                    var response=(await udp.ReceiveAsync(deadline.Token)).Buffer;
                    if(response.Length>=12&&response[0]==bytes[0]&&response[1]==bytes[1]&&(response[2]&128)!=0
                        &&(response[3]&15)==0&&BinaryPrimitives.ReadUInt16BigEndian(response.AsSpan(6))>0)return;
                }
                catch(Exception error) when(error is SocketException or OperationCanceledException){budget.Token.ThrowIfCancellationRequested();}
            }
        }
        catch(OperationCanceledException) when(!cancellationToken.IsCancellationRequested){throw new ClientFault("VPN_DNS_FAILED");}
        cancellationToken.ThrowIfCancellationRequested();throw new ClientFault("VPN_DNS_FAILED");
    }
}
