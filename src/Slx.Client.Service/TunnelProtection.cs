using Slx.Client.Core;
using Slx.Client.Windows;
namespace Slx.Client.Service;
internal sealed partial class TunnelEngine
{
    private readonly TunnelIntentStore intents=new();
    private readonly StartupOffCleanup startupOffCleanup=new();
    private TrafficGuard? trafficGuard;
    private ConnectPlan? pinnedPlan;
    private bool desiredConnected,protectionActive;
    private volatile bool confirmedUnprotected;
    private long retryAt,healthAt;
    public string? OwnerSid=>ownerSid;
    public bool IsConfirmedOff=>confirmedUnprotected&&state.Phase==TunnelPhase.Disconnected&&!protectionActive&&ownerSid is null
        &&!desiredConnected&&!offCleanup.Pending&&!startupOffCleanup.Pending&&child is null&&ownedAdapterId is null&&currentPlan is null;
    public async Task RestoreAsync(CancellationToken cancellationToken, bool suppressResume = false)
    {
        await lifecycle.WaitAsync(cancellationToken);
        try
        {
            var saved=intents.Load();
            if(saved==null){startupOffCleanup.Begin();TryStartupOffCleanup();return;}
            if(suppressResume)
            {state=new(TunnelPhase.Faulted,DateTimeOffset.UtcNow,Code:"UPDATE_MAINTENANCE_UNCONFIRMED");return;}
            startupOffCleanup.Cancel();
            pinnedPlan=saved.Plan;currentPlan=saved.EndpointHost is null?saved.Plan:saved.Plan with{Profile=saved.Plan.Profile with{Host=saved.EndpointHost}};ownerSid=saved.OwnerSid;desiredConnected=true;
            var restored=ExactApplicationPlan.Prepare(RestoredTunnelPlan.Refresh(saved,cancellationToken),cancellationToken);
            pinnedPlan=restored.Plan;
            currentPlan=restored.EndpointHost is null?restored.Plan:restored.Plan with{Profile=restored.Plan.Profile with{Host=restored.EndpointHost}};
            // Preserve old rules until the complete refreshed native-ID set passes and replacement commits.
            ArmProtection(restored.Plan);
            if(!ReferenceEquals(restored,saved))intents.Save(restored);
            state=new(TunnelPhase.Reconnecting,DateTimeOffset.UtcNow,restored.Plan.Profile.Name,Mode:restored.Plan.Mode,ProtectionActive:true);
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
            confirmedUnprotected=false;
            protectionActive=true;
        }
        catch{CloseProtectionSession();throw;}
    }
    private bool TryStartupOffCleanup()
    {
        if(!startupOffCleanup.Pending)return false;
        // A new connection owns protection as soon as its intent is adopted. Startup cleanup
        // must never release that policy, even if later connection readiness fails.
        if(desiredConnected||currentPlan is not null){startupOffCleanup.Cancel();return false;}
        try
        {
            if(startupOffCleanup.TryRelease(ReleaseProtection))
            {state=new(TunnelPhase.Disconnected,DateTimeOffset.UtcNow);confirmedUnprotected=true;}
        }
        catch(ClientFault)
        {
            state=new(TunnelPhase.Faulted,DateTimeOffset.UtcNow,Code:StartupOffCleanup.PendingCode,ProtectionActive:protectionActive);
        }
        return true;
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
        using var stopping = connectionWork.RequestStop();
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
