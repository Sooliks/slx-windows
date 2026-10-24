using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Slx.Client.Core;

namespace Slx.Client.Service;

internal sealed partial class ControlDispatcher(IControlTunnelEngine engine, ISessionGuard sessions)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Dictionary<Guid, Receipt> receipts = new();
    private string? owner;
    private sealed record Receipt(string Fingerprint, PipeReply Reply, long Stamp);
    internal const int ReceiptCapacity = 4096;
    // Receipts used to live for the whole service lifetime: after 4096 mutations every connect/reconnect/app update
    // was refused with IPC_RECEIPT_LIMIT until the service (in practice the PC) restarted. The UI never resends a
    // request ID, so replay protection only needs a bounded recent window; the capacity still caps bursts inside it.
    internal static readonly TimeSpan ReceiptRetention = TimeSpan.FromMinutes(10);
    internal Func<long> Clock { get; init; } = Stopwatch.GetTimestamp;

    public async Task<PipeReply> DispatchAsync(string sid, PipeRequest request, CancellationToken cancellationToken)
    {
        if (request.Version != PipeContract.Version || request.RequestId == Guid.Empty
            || request.Action is not ("status" or "connect" or "connect-if-idle" or "disconnect" or "reconnect" or "update-applications"))
            return Rejected("IPC_INVALID");
        if (request.Action == "status") return Observe(sid, request);
        var mutation = AdmitMutation(sid, request);
        using var identity = mutation.Admission;
        if (mutation.Reply is { } denied) return denied;
        var off = request.Action == "disconnect" ? PrepareDisconnect(sid, request) : default;
        using var admission = off.Admission;
        if (off.Reply is { } rejected) return rejected;
        await gate.WaitAsync(cancellationToken);
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            string fingerprint = Fingerprint(sid, request);
            lock (observation)
            {
                RefreshOwner();
                if (owner is not null && owner != sid) return Rejected("CONTROLLER_OWNED");
                PruneReceipts();
                if (receipts.TryGetValue(request.RequestId, out var prior))
                    return prior.Fingerprint == fingerprint ? prior.Reply with { Status = engine.Status }
                        : Rejected("IPC_REPLAY_MISMATCH");
            }
            if ((request.Action is "connect" or "connect-if-idle" or "update-applications") && request.Plan is null
                || request.Action == "disconnect" && request.Plan is not null)
                return Rejected("IPC_INVALID");
            bool receiptAvailable;
            lock (observation) receiptAvailable = receipts.Count < ReceiptCapacity;
            // Explicit off is idempotent and must remain available when the replay cache is full.
            // Existing receipt fingerprints were checked first; no new connect/reconnect can bypass the cap.
            if (!receiptAvailable && request.Action != "disconnect") return Rejected("IPC_RECEIPT_LIMIT");
            if (request.Plan is not null)
            {
                try { PlanValidator.Validate(request.Plan); }
                catch (ClientFault fault) { return Rejected(fault.Code); }
            }
            if (request.Action is "connect" or "connect-if-idle" or "reconnect" or "update-applications")
            {
                try { sessions.RequireOwner(sid); }
                catch (ClientFault fault) { return Rejected(fault.Code); }
            }
            var observed=engine.Status;
            if(request.Action=="connect-if-idle"&&(observed.Phase!=TunnelPhase.Disconnected||observed.ProtectionActive))
            {
                // Remember the no-op too: replaying this request after explicit off must never connect later.
                var unchanged=new PipeReply(true,"OK",observed);
                lock (observation) receipts.Add(request.RequestId,new(fingerprint,unchanged,Clock()));
                return unchanged;
            }
            lock (observation)
            {
                if (request.Plan is not null && request.Action != "update-applications") owner ??= sid;
                active = new(request.RequestId, fingerprint, request.Action, operation);
                if (request.Action != "disconnect" && pendingStops != 0) operation.Cancel();
            }
            PipeReply result;
            try
            {
                if (request.Action == "disconnect") await engine.StopAsync(operation.Token);
                else if (request.Action == "update-applications") await engine.UpdateApplicationsAsync(request.Plan!, sid, operation.Token);
                else
                {
                    if (request.Plan is not null) await engine.StartAsync(request.Plan, sid, operation.Token);
                    else await engine.ReconnectAsync(operation.Token);
                }
                result = new(true, "OK", engine.Status);
            }
            catch (ClientFault fault) { result = new(false, fault.Code, engine.Status); }
            catch (OperationCanceledException) { result = new(false, "OPERATION_INTERRUPTED", engine.Status); }
            catch (Exception) { result = new(false, "SERVICE_FAILURE", engine.Status); }
            lock (observation)
            {
                if ((request.Action != "disconnect" || result.Success)
                    && result.Status.Phase == TunnelPhase.Disconnected && !result.Status.ProtectionActive
                    && engine.OwnerSid is null) owner = null;
                if (receiptAvailable) receipts.Add(request.RequestId, new(fingerprint, result, Clock()));
            }
            return result;
        }
        finally
        {
            lock (observation) active = null;
            gate.Release();
        }
    }

    private void PruneReceipts()
    {
        if (receipts.Count == 0) return;
        long cutoff = Clock() - (long)(ReceiptRetention.TotalSeconds * Stopwatch.Frequency);
        foreach (var expired in receipts.Where(pair => pair.Value.Stamp < cutoff).Select(pair => pair.Key).ToArray())
            receipts.Remove(expired);
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
