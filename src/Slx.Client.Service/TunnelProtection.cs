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
            trafficGuard??=new();
            var saved=intents.Load();
            if(saved==null){trafficGuard.Remove();return;}
            pinnedPlan=saved.Plan;currentPlan=saved.EndpointHost is null?saved.Plan:saved.Plan with{Profile=saved.Plan.Profile with{Host=saved.EndpointHost}};ownerSid=saved.OwnerSid;desiredConnected=true;
            ArmProtection(saved.Plan);
            state=new(TunnelPhase.Reconnecting,DateTimeOffset.UtcNow,saved.Plan.Profile.Name,Mode:saved.Plan.Mode,ProtectionActive:true);
        }
        finally{lifecycle.Release();}
    }
    private void ArmProtection(ConnectPlan plan,string? adapter=null)
    {
        trafficGuard??=new();
        trafficGuard.Apply(plan,RuntimeTrust.Verify(),Environment.ProcessPath??throw new ClientFault("SERVICE_IDENTITY_FAILED"),adapter);
        protectionActive=true;
    }
    public async Task ShutdownAsync()
    {
        await lifecycle.WaitAsync();
        try{await StopChildAsync();}
        finally{trafficGuard?.Dispose();trafficGuard=null;lifecycle.Release();}
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
