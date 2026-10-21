using Slx.Client.Core;
namespace Slx.Client.Service;
internal enum ApplicationProtectionStage { Before, Union, After }
/// <summary>Cross-owner ordering. An uncertain core result retains union protection and never replays a mutation.</summary>
internal sealed class ApplicationUpdateTransaction(ConnectPlan before, ConnectPlan after,
    Action<ApplicationProtectionStage> protect, Action<bool> save,
    Func<ConnectPlan, CancellationToken, Task> replace, Func<CancellationToken, Task<string>> readRevision)
{
    public bool Finished { get; private set; }
    public bool Applied { get; private set; }
    public bool Attempted { get; private set; }
    public string? FailureCode { get; private set; }
    private bool? coreApplied;
    public async Task StartAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        protect(ApplicationProtectionStage.Union);
        try { save(true); }
        catch
        {
            // No core mutation has been sent: old routing is still authoritative.
            protect(ApplicationProtectionStage.Before);
            throw;
        }
        Attempted = true;
        try { await replace(after, token); coreApplied = true; }
        catch (Exception error) when (error is ClientFault or OperationCanceledException or IOException
            or HttpRequestException)
        { FailureCode = error is ClientFault fault ? fault.Code : "ROUTING_UPDATE_UNCONFIRMED"; }
        await ReconcileAsync();
    }
    public async Task ReconcileAsync()
    {
        if (Finished) return;
        try
        {
            if (coreApplied is null)
            {
                using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(6));
                var revision = await readRevision(budget.Token);
                if (revision == XrayRouteRevision.For(after)) coreApplied = true;
                else if (revision == XrayRouteRevision.For(before)) coreApplied = false;
                else return;
            }
            // Intent is durable before releasing any blocking rule. A crash resumes the adopted desired plan.
            save(coreApplied.Value);
            protect(coreApplied.Value ? ApplicationProtectionStage.After : ApplicationProtectionStage.Before);
            Applied = coreApplied.Value;
            Finished = true;
            FailureCode = Applied ? null : "ROUTING_UPDATE_FAILED";
        }
        catch (Exception error) when (error is ClientFault or OperationCanceledException or IOException
            or HttpRequestException or UnauthorizedAccessException or System.Security.Cryptography.CryptographicException)
        { FailureCode = error is ClientFault fault ? fault.Code : "ROUTING_UPDATE_UNCONFIRMED"; }
    }
}
