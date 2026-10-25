using System.ComponentModel;
using System.Security.Principal;
using Slx.Client.Core;
using Slx.Client.Windows;

namespace Slx.Client.Service;

/// <summary>Fixed installed preflight used by Inno after UAC; no command, target path or configuration input.</summary>
internal static class UpdateClaimCommand
{
    internal static bool Requested(string[] args) => args.Length > 0 && args[0] == "--claim-update";
    internal static bool TrySession(string[] args, out Guid id)
    {
        id = Guid.Empty;
        return args is ["--claim-update", var value] && Guid.TryParseExact(value, "N", out id) && id != Guid.Empty;
    }
    internal static async Task<int> RunAsync(string[] args)
    {
        if (!TrySession(args, out _)) return 2;
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            bool administrator = new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
            return await RunAsync(args, administrator, new ServiceClient().SendAsync);
        }
        catch (Exception error) when (error is UnauthorizedAccessException or System.Security.SecurityException
            or InvalidOperationException or Win32Exception) { return 1; }
    }
    internal static async Task<int> RunAsync(string[] args, bool administrator,
        Func<PipeRequest, CancellationToken, Task<PipeReply>> send)
    {
        if (!administrator || !TrySession(args, out var id)) return 2;
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            var reply = await send(new(PipeContract.Version, "claim-update", Guid.NewGuid(), UpdateSessionId: id), budget.Token)
                .WaitAsync(budget.Token);
            return reply.Success && reply.Status.Phase == TunnelPhase.Disconnected && !reply.Status.ProtectionActive
                && reply.Status.UpdateMaintenance is { Claimed: true } maintenance && maintenance.SessionId == id
                && maintenance.ExpiresAt > DateTimeOffset.UtcNow ? 0 : 1;
        }
        catch (Exception error) when (error is ClientFault or OperationCanceledException or IOException
            or UnauthorizedAccessException or Win32Exception or InvalidOperationException) { return 1; }
    }
}
