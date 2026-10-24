namespace Slx.Client.Tests;

internal static class DnsProbeFixtures
{
    internal static byte[] Reply(byte[] query, int responseCode = 0, bool answer = true)
    {
        var response = query.ToList();
        response[2] = 0x81;
        response[3] = (byte)(0x80 | responseCode);
        response[7] = answer ? (byte)1 : (byte)0;
        if (answer) response.AddRange(new byte[]
        {
            0xc0, 12, 0, 1, 0, 1, 0, 0, 1, 44, 0, 4, 93, 184, 216, 34
        });
        return response.ToArray();
    }
}
