using System.IO.Pipes;
using System.Diagnostics;
using System.Security.AccessControl;
using System.Security.Principal;
using Slx.Client.Core;
using Slx.Client.Service;
using Slx.Client.Windows;
using Xunit;

namespace Slx.Client.Tests;

/// <summary>Real Windows named-pipe OS contracts. No installed service, profiles, core or TUN.</summary>
public sealed class WindowsPipeTests
{
    [Fact]
    public async Task DelayedClientWriteMakesCallerIdentityAvailableBeforeDispatch()
    {
        string originalSid = WindowsIdentity.GetCurrent().User!.Value;
        string name = "SLX.Tests." + Guid.NewGuid().ToString("N");
        await using var server = ControlPipe.Create(name);
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var read = Task.Run(async () =>
        {
            await server.WaitForConnectionAsync(budget.Token);
            return await ControlPipe.ReadRequestAsync(server, budget.Token);
        });
        await using var client = new NamedPipeClientStream(".", name, PipeDirection.InOut,
            PipeOptions.Asynchronous, TokenImpersonationLevel.Impersonation);
        await client.ConnectAsync(budget.Token);
        await Task.Delay(150, budget.Token);
        var request = new PipeRequest(PipeContract.Version, "status", Guid.NewGuid());
        await PipeFraming.WriteAsync(client, request, budget.Token);
        var actual = await read;
        Assert.Equal(WindowsIdentity.GetCurrent().User!.Value, actual.Sid);
        Assert.Equal(request, actual.Request);
        Assert.Equal(originalSid, WindowsIdentity.GetCurrent().User!.Value);
    }

    [Fact]
    public async Task ProductionAclHasLocalInteractiveAccessAndRejectsNetworkPrincipals()
    {
        await using var pipe = ControlPipe.Create("SLX.Tests." + Guid.NewGuid().ToString("N"));
        var acl = pipe.GetAccessControl();
        Assert.True(acl.AreAccessRulesProtected);
        var rules = acl.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<PipeAccessRule>().ToArray();
        foreach (var type in new[] { WellKnownSidType.NetworkSid, WellKnownSidType.AnonymousSid })
            Assert.Contains(rules, rule => rule.IdentityReference.Equals(new SecurityIdentifier(type, null))
                && rule.AccessControlType == AccessControlType.Deny
                && rule.PipeAccessRights == PipeAccessRights.FullControl);
        Assert.Contains(rules, rule => rule.IdentityReference.Equals(
            new SecurityIdentifier(WellKnownSidType.InteractiveSid, null))
            && rule.AccessControlType == AccessControlType.Allow
            && (rule.PipeAccessRights & PipeAccessRights.ReadWrite) == PipeAccessRights.ReadWrite);
    }

    [Fact]
    public async Task WindowsReportsTheActualServerPidAndRejectsAnImpostorServicePipe()
    {
        string name = "SLX.Tests." + Guid.NewGuid().ToString("N");
        await using var server = ControlPipe.Create(name);
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var connected = server.WaitForConnectionAsync(budget.Token);
        await using var client = new NamedPipeClientStream(".", name, PipeDirection.InOut,
            PipeOptions.Asynchronous, TokenImpersonationLevel.Impersonation);
        await client.ConnectAsync(budget.Token);
        await connected;
        Assert.Equal((uint)Environment.ProcessId, PipeServiceIdentity.ServerProcessId(client.SafePipeHandle));
        Assert.Equal("SERVICE_IDENTITY_FAILED", Assert.Throws<ClientFault>(
            () => PipeServiceIdentity.Validate(client.SafePipeHandle)).Code);
    }

    [Fact]
    public void NativeScmStatusLayoutReturnsTheRunningWindowsRpcServiceProcess()
    {
        uint pid = PipeServiceIdentity.RunningServiceProcessId("RpcSs");
        var processes = Process.GetProcesses();
        try { Assert.Contains(processes, process => process.Id == pid && process.SessionId == 0); }
        finally { foreach (var process in processes) process.Dispose(); }
        Assert.NotEqual((uint)Environment.ProcessId, pid);
    }

    [Fact]
    public async Task PersistentNativePipeCanReadAndReplyAcrossSequentialConnections()
    {
        string name = "SLX.Tests." + Guid.NewGuid().ToString("N");
        await using var server = ControlPipe.Create(name);
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var dispatcher = new ControlDispatcher(new TunnelEngine(), new WindowsSessionGuard());
        var serving = Task.Run(async () =>
        {
            for (int index = 0; index < 2; index++)
            {
                await server.WaitForConnectionAsync(budget.Token);
                var caller = await ControlPipe.ReadRequestAsync(server, budget.Token);
                var reply = await dispatcher.DispatchAsync(caller.Sid, caller.Request, budget.Token);
                await PipeFraming.WriteReplyAndWaitAckAsync(server, reply, budget.Token);
                server.Disconnect();
            }
        });
        for (int index = 0; index < 2; index++)
        {
            await using var client = new NamedPipeClientStream(".", name, PipeDirection.InOut,
                PipeOptions.Asynchronous, TokenImpersonationLevel.Impersonation);
            await client.ConnectAsync(budget.Token);
            await PipeFraming.WriteAsync(client, new PipeRequest(PipeContract.Version, "status", Guid.NewGuid()), budget.Token);
            var reply = await PipeFraming.ReceiveReplyAndAcknowledgeAsync(client, budget.Token);
            Assert.True(reply.Success);
            Assert.Equal(TunnelPhase.Disconnected, reply.Status.Phase);
            Assert.Null(reply.Status.ProfileName);
        }
        await serving;
    }

    [Fact]
    public async Task InvalidNativeRequestReceivesABoundedNonSensitiveErrorReply()
    {
        string name = "SLX.Tests." + Guid.NewGuid().ToString("N");
        await using var server = ControlPipe.Create(name);
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var serving = Task.Run(async () =>
        {
            await server.WaitForConnectionAsync(budget.Token);
            try { await ControlPipe.ReadRequestAsync(server, budget.Token); }
            catch (ClientFault fault) { await ControlWorker.RejectSafelyAsync(server, fault.Code); }
        });
        await using var client = new NamedPipeClientStream(".", name, PipeDirection.InOut,
            PipeOptions.Asynchronous, TokenImpersonationLevel.Impersonation);
        await client.ConnectAsync(budget.Token);
        byte[] header = new byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(header, PipeContract.MaxBytes + 1);
        await client.WriteAsync(header, budget.Token);
        await client.FlushAsync(budget.Token);
        var reply = await PipeFraming.ReceiveReplyAndAcknowledgeAsync(client, budget.Token);
        Assert.False(reply.Success);
        Assert.Equal("IPC_PAYLOAD_LIMIT", reply.Code);
        Assert.Null(reply.Status.ProfileName);
        Assert.Null(reply.Status.Mode);
        await serving;
    }
}
