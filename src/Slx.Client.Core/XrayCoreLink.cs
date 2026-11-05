using System.Security.Cryptography;
using System.Text;

namespace Slx.Client.Core;

/// <summary>Loopback-only authenticated (VLESS) link from the long-lived TUN process to the restartable VPN core.</summary>
public sealed record XrayCoreLink(int Port, string User, string Password, string CorePath)
{
    public override string ToString() => "XrayCoreLink { Credentials = [redacted] }";

    /// <summary>Deterministic VLESS id derived from both per-connection secrets.</summary>
    public string Id => new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(User + ":" + Password)).AsSpan(0, 16)).ToString("D");

    internal void Validate()
    {
        if (Port is < 1024 or > 65535 || !XrayConfigBuilder.SafeProbe(User) || !XrayConfigBuilder.SafeProbe(Password)
            || !PlanValidator.IsExecutablePath(CorePath)) throw new ClientFault("INVALID_PLAN");
    }
}
