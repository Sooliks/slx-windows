using Slx.Client.Core;

namespace Slx.Client.Service;

internal interface IControlTunnelEngine
{
    TunnelStatus Status { get; }
    string? OwnerSid => null;
    bool IsConfirmedOff => false;
    Task StartAsync(ConnectPlan plan, string sid, CancellationToken cancellationToken);
    Task ReconnectAsync(CancellationToken cancellationToken);
    /// <summary>Explicit user reconnect with a plan: resolves the server again and starts from its first transport.</summary>
    Task RestartAsync(ConnectPlan plan, string sid, CancellationToken cancellationToken) => StartAsync(plan, sid, cancellationToken);
    /// <summary>Best-effort: a running connection learns the other transports of its server from the UI's plan.</summary>
    Task AdoptAlternatesAsync(ConnectPlan plan, string sid, CancellationToken cancellationToken) => Task.CompletedTask;
    Task UpdateApplicationsAsync(ConnectPlan plan, string sid, CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
}

internal interface ISessionGuard { void RequireOwner(string sid); }
internal sealed class WindowsSessionGuard : ISessionGuard
{
    public void RequireOwner(string sid) => InteractiveSessions.RequireSingleOwner(sid);
}
