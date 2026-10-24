using System.IO.Pipes;
using Microsoft.Extensions.Hosting;
using Slx.Client.Core;
using Slx.Client.Windows;

namespace Slx.Client.Service;

internal sealed class ControlWorker(ControlDispatcher dispatcher, TunnelEngine engine) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await engine.RestoreAsync(stoppingToken);
        try { await Task.WhenAll(RunPipesAsync(stoppingToken), MonitorAsync(stoppingToken)); }
        finally { await engine.ShutdownAsync(); }
    }

    private async Task RunPipesAsync(CancellationToken stoppingToken)
    {
        var pipes = new List<NamedPipeServerStream>();
        try
        {
            // Keep the first-instance handle alive for the pool lifetime. Each worker owns one
            // client at a time, while status/off can use a different slot during a long connect.
            for (int index = 0; index < ControlPipe.MaxInstances; index++)
                pipes.Add(ControlPipe.Create(PipeContract.Name, firstInstance: index == 0));
            await Task.WhenAll(pipes.Select(pipe => RunPipeAsync(pipe, stoppingToken)));
        }
        finally { foreach (var pipe in pipes) await pipe.DisposeAsync(); }
    }

    private async Task RunPipeAsync(NamedPipeServerStream pipe, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await pipe.WaitForConnectionAsync(stoppingToken);
                using var readBudget = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                readBudget.CancelAfter(TimeSpan.FromSeconds(5));
                var caller = await ControlPipe.ReadRequestAsync(pipe, readBudget.Token);
                using var budget = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                // DNS + tunnel readiness + owned cleanup can legitimately exceed the former 50s
                // budget. Other pipe slots still serve status/off immediately during this wait.
                budget.CancelAfter(TimeSpan.FromSeconds(caller.Request.Action == "status" ? 5 : 75));
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
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
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
