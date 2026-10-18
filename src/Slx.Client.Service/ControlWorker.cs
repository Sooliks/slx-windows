using System.IO.Pipes;
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
        await using var pipe = ControlPipe.Create(PipeContract.Name);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await pipe.WaitForConnectionAsync(stoppingToken);
                using var budget = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                budget.CancelAfter(TimeSpan.FromSeconds(50));
                var caller = await ControlPipe.ReadRequestAsync(pipe, budget.Token);
                var reply = await dispatcher.DispatchAsync(caller.Sid, caller.Request, budget.Token);
                await PipeFraming.WriteReplyAndWaitAckAsync(pipe, reply, budget.Token);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (ClientFault fault) { await RejectSafelyAsync(pipe, fault.Code); }
            catch (OperationCanceledException) { await RejectSafelyAsync(pipe, "IPC_REQUEST_TIMEOUT"); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException)
            { await RejectSafelyAsync(pipe, "IPC_REQUEST_FAILED"); }
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

    internal static async Task RejectSafelyAsync(NamedPipeServerStream pipe, string code)
    {
        if (!pipe.IsConnected) return;
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        try
        {
            await PipeFraming.WriteReplyAndWaitAckAsync(pipe, new PipeReply(false, code,
                new TunnelStatus(TunnelPhase.Disconnected, DateTimeOffset.UtcNow)), budget.Token);
        }
        catch (Exception error) when (error is IOException or InvalidOperationException or OperationCanceledException) { }
    }
}
