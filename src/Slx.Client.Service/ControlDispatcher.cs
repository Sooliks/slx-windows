using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Slx.Client.Core;

namespace Slx.Client.Service;

internal sealed class ControlDispatcher(IControlTunnelEngine engine, ISessionGuard sessions)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Dictionary<Guid, Receipt> receipts = new();
    private string? owner;
    private sealed record Receipt(string Fingerprint, PipeReply Reply);

    public async Task<PipeReply> DispatchAsync(string sid, PipeRequest request, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (request.Version != PipeContract.Version || request.RequestId == Guid.Empty
                || request.Action is not ("status" or "connect" or "connect-if-idle" or "disconnect" or "reconnect"))
                return Rejected("IPC_INVALID");
            owner ??= engine.OwnerSid;
            if (owner is not null && owner != sid) return Rejected("CONTROLLER_OWNED");
            if (request.Action == "status")
                return request.Plan is null ? new PipeReply(true, "OK", engine.Status) : Rejected("IPC_INVALID");
            string fingerprint = Fingerprint(sid, request);
            if (receipts.TryGetValue(request.RequestId, out var prior))
                return prior.Fingerprint == fingerprint ? prior.Reply with { Status = engine.Status }
                    : Rejected("IPC_REPLAY_MISMATCH");
            if ((request.Action is "connect" or "connect-if-idle") && request.Plan is null
                || request.Action == "disconnect" && request.Plan is not null)
                return Rejected("IPC_INVALID");
            bool receiptAvailable = receipts.Count < 4096;
            // Explicit off is idempotent and must remain available when the replay cache is full.
            // Existing receipt fingerprints were checked first; no new connect/reconnect can bypass the cap.
            if (!receiptAvailable && request.Action != "disconnect") return Rejected("IPC_RECEIPT_LIMIT");
            if (request.Plan is not null)
            {
                try { PlanValidator.Validate(request.Plan); }
                catch (ClientFault fault) { return Rejected(fault.Code); }
            }
            if (request.Action is "connect" or "connect-if-idle" or "reconnect")
            {
                try { sessions.RequireOwner(sid); }
                catch (ClientFault fault) { return Rejected(fault.Code); }
            }
            var observed=engine.Status;
            if(request.Action=="connect-if-idle"&&(observed.Phase!=TunnelPhase.Disconnected||observed.ProtectionActive))
            {
                // Remember the no-op too: replaying this request after explicit off must never connect later.
                var unchanged=new PipeReply(true,"OK",observed);
                receipts.Add(request.RequestId,new(fingerprint,unchanged));
                return unchanged;
            }
            if (request.Plan is not null) owner ??= sid;
            PipeReply result;
            try
            {
                if (request.Action == "disconnect") await engine.StopAsync(cancellationToken);
                else
                {
                    if (request.Plan is not null) await engine.StartAsync(request.Plan, sid, cancellationToken);
                    else await engine.ReconnectAsync(cancellationToken);
                }
                result = new(true, "OK", engine.Status);
            }
            catch (ClientFault fault) { result = new(false, fault.Code, engine.Status); }
            catch (OperationCanceledException) { result = new(false, "OPERATION_INTERRUPTED", engine.Status); }
            catch (Exception) { result = new(false, "SERVICE_FAILURE", engine.Status); }
            if (request.Action == "disconnect" && result.Success
                && result.Status.Phase == TunnelPhase.Disconnected && !result.Status.ProtectionActive) owner = null;
            if (receiptAvailable) receipts.Add(request.RequestId, new(fingerprint, result));
            return result;
        }
        finally { gate.Release(); }
    }

    private static string Fingerprint(string sid, PipeRequest request)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(sid + JsonSerializer.Serialize(request));
        try { return Convert.ToHexString(SHA256.HashData(bytes)); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    private static PipeReply Rejected(string code) => new(false, code,
        new TunnelStatus(TunnelPhase.Disconnected, DateTimeOffset.UtcNow));
}
