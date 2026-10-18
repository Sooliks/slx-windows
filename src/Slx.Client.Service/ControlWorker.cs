using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Extensions.Hosting;
using Slx.Client.Core;
using Slx.Client.Windows;

namespace Slx.Client.Service;

internal sealed class ControlWorker(ControlDispatcher dispatcher, TunnelEngine engine) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.WhenAll(RunPipeAsync(stoppingToken), MonitorAsync(stoppingToken)); }
        finally { await engine.StopAsync(CancellationToken.None); }
    }

    private async Task RunPipeAsync(CancellationToken stoppingToken)
    {
        await using var pipe = CreatePipe();
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await pipe.WaitForConnectionAsync(stoppingToken);
                using var budget = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                budget.CancelAfter(TimeSpan.FromSeconds(50));
                string? sid = null;
                pipe.RunAsClient(() => sid = WindowsIdentity.GetCurrent().User?.Value);
                var request = await PipeFraming.ReadAsync<PipeRequest>(pipe, budget.Token);
                var reply = sid is null ? Rejected("IPC_CALLER_INVALID")
                    : await dispatcher.DispatchAsync(sid, request, budget.Token);
                await PipeFraming.WriteAsync(pipe, reply, budget.Token);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException
                or OperationCanceledException or ClientFault or InvalidOperationException)
            { /* Invalid or abandoned requests cannot terminate the service. */ }
            finally { if (pipe.IsConnected) pipe.Disconnect(); }
        }
    }

    private async Task MonitorAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
                await engine.CheckHealthAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    private static NamedPipeServerStream CreatePipe()
    {
        var acl = new PipeSecurity();
        acl.SetAccessRuleProtection(true, false);
        foreach (WellKnownSidType denied in new[] { WellKnownSidType.NetworkSid, WellKnownSidType.AnonymousSid })
            acl.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(denied, null),
                PipeAccessRights.FullControl, AccessControlType.Deny));
        acl.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.InteractiveSid, null),
            PipeAccessRights.ReadWrite | PipeAccessRights.Synchronize, AccessControlType.Allow));
        foreach (WellKnownSidType allowed in new[] { WellKnownSidType.LocalSystemSid, WellKnownSidType.BuiltinAdministratorsSid })
            acl.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(allowed, null),
                PipeAccessRights.FullControl, AccessControlType.Allow));
        return NamedPipeServerStreamAcl.Create(PipeContract.Name, PipeDirection.InOut, 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.WriteThrough | PipeOptions.FirstPipeInstance,
            PipeContract.MaxBytes, PipeContract.MaxBytes, acl);
    }

    private static PipeReply Rejected(string code) => new(false, code,
        new TunnelStatus(TunnelPhase.Disconnected, DateTimeOffset.UtcNow));
}
