namespace Slx.Client.Core;

/// <summary>Loopback-only authenticated link from the long-lived TUN process to the restartable VPN core.</summary>
public sealed record XrayCoreLink(int Port, string User, string Password, string CorePath)
{
    public override string ToString() => "XrayCoreLink { Credentials = [redacted] }";

    internal void Validate()
    {
        if (Port is < 1024 or > 65535 || !XrayConfigBuilder.SafeProbe(User) || !XrayConfigBuilder.SafeProbe(Password)
            || !PlanValidator.IsExecutablePath(CorePath)) throw new ClientFault("INVALID_PLAN");
    }
}
