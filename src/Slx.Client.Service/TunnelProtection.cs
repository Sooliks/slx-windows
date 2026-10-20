using Slx.Client.Core;
using Slx.Client.Windows;
namespace Slx.Client.Service;
internal sealed partial class TunnelEngine
{
    private readonly TunnelIntentStore intents=new();
    private TrafficGuard? trafficGuard;
    private ConnectPlan? pinnedPlan;
    private bool desiredConnected,protectionActive;
    private long retryAt,healthAt;
    public string? OwnerSid=>ownerSid;
    public async Task RestoreAsync(CancellationToken cancellationToken)
    {
        await lifecycle.WaitAsync(cancellationToken);
        try
        {
            var saved=intents.Load();
            if(saved==null){ReleaseProtection();return;}
            pinnedPlan=saved.Plan;currentPlan=saved.EndpointHost is null?saved.Plan:saved.Plan with{Profile=saved.Plan.Profile with{Host=saved.EndpointHost}};ownerSid=saved.OwnerSid;desiredConnected=true;
            ArmProtection(saved.Plan);
            state=new(TunnelPhase.Reconnecting,DateTimeOffset.UtcNow,saved.Plan.Profile.Name,Mode:saved.Plan.Mode,ProtectionActive:true);
        }
        catch(Exception error) when(error is ClientFault or System.IO.IOException or UnauthorizedAccessException
            or System.Security.Cryptography.CryptographicException)
        {
            // Keep the intent and previous persistent rules. A failed restore must not prevent local
            // status/disconnect access or the existing paced recovery loop from ever starting.
            string code=error is ClientFault fault?fault.Code:"PROTECTION_STORAGE_FAILED";
            if(desiredConnected)ScheduleRecovery(code);
            else state=new(TunnelPhase.Faulted,DateTimeOffset.UtcNow,Code:code,ProtectionActive:protectionActive);
        }
        finally{lifecycle.Release();}
    }
    private void ArmProtection(ConnectPlan plan,string? adapter=null)
    {
        trafficGuard??=new();
        try
        {
            trafficGuard.Apply(plan,RuntimeTrust.Verify(),Environment.ProcessPath??throw new ClientFault("SERVICE_IDENTITY_FAILED"),adapter);
            protectionActive=true;
        }
        catch{CloseProtectionSession();throw;}
    }
    private void ReleaseProtection()
    {
        trafficGuard??=new();
        try{trafficGuard.Remove();protectionActive=false;}
        catch{CloseProtectionSession();throw;}
    }
    private void CloseProtectionSession()
    {
        // A BFE restart can invalidate the RPC session. The next attempt opens a new one;
        // closing a session never removes the persistent provider-owned blocking policy.
        trafficGuard?.Dispose();trafficGuard=null;
    }
    public async Task ShutdownAsync()
    {
        await lifecycle.WaitAsync();
        try{await StopChildAsync();}
        finally{CloseProtectionSession();lifecycle.Release();}
        // Persistent WFP policy and encrypted intent survive host shutdown. Only explicit StopAsync releases them.
    }
    private void ScheduleRecovery(string code)
    {
        recoveryAttempts=Math.Min(recoveryAttempts+1,30);
        retryAt=System.Diagnostics.Stopwatch.GetTimestamp()+(long)(RecoveryBackoff.Delay(recoveryAttempts).TotalSeconds*System.Diagnostics.Stopwatch.Frequency);
        state=new(TunnelPhase.Reconnecting,DateTimeOffset.UtcNow,currentPlan?.Profile.Name,code,
            Mode:currentPlan?.Mode,ProtectionActive:protectionActive);
    }
}
