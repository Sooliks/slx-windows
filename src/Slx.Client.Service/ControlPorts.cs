using Slx.Client.Core;

namespace Slx.Client.Service;

internal interface IControlTunnelEngine
{
    TunnelStatus Status { get; }
    string? OwnerSid => null;
    Task StartAsync(ConnectPlan plan, string sid, CancellationToken cancellationToken);
    Task ReconnectAsync(CancellationToken cancellationToken);
    Task UpdateApplicationsAsync(ConnectPlan plan, string sid, CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
}

internal interface ISessionGuard { void RequireOwner(string sid); }
internal sealed class WindowsSessionGuard : ISessionGuard
{
    public void RequireOwner(string sid) => InteractiveSessions.RequireSingleOwner(sid);
}
