using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using Slx.Client.Core;
using Slx.Client.Service;
using Slx.Client.Windows;
using Xunit;

namespace Slx.Client.Tests;

/// <summary>Actual Windows pipe overlap; no installed service, core, WFP policy or network changes.</summary>
public sealed class PipeConcurrencyTests
{
    [Fact]
    public async Task AConnectedSlowRequestDoesNotPreventASecondClientFromReceivingStatus()
    {
        string name = "SLX.Tests." + Guid.NewGuid().ToString("N");
        await using var first = FixtureServer(name);
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var received = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var expected = new PipeReply(true, "OK", new(TunnelPhase.Disconnected, DateTimeOffset.UtcNow));
        var servingFirst = Task.Run(async () =>
        {
            await first.WaitForConnectionAsync(budget.Token);
            await ControlPipe.ReadRequestAsync(first, budget.Token);
            received.SetResult();
            await release.Task.WaitAsync(budget.Token);
            await PipeFraming.WriteReplyAndWaitAckAsync(first, expected, budget.Token);
        });
        await using var slow = Client(name);
        await slow.ConnectAsync(budget.Token);
        await PipeFraming.WriteAsync(slow, Request(), budget.Token);
        await received.Task.WaitAsync(budget.Token);
        // Create the sibling after the first request arrives, so the occupied instance is unambiguous.
        await using var second = FixtureServer(name, firstInstance: false);
        var servingSecond = Task.Run(async () =>
        {
            await second.WaitForConnectionAsync(budget.Token);
            await ControlPipe.ReadRequestAsync(second, budget.Token);
            await PipeFraming.WriteReplyAndWaitAckAsync(second, expected, budget.Token);
        });
        try
        {
            await using var status = Client(name);
            await status.ConnectAsync(budget.Token);
            await PipeFraming.WriteAsync(status, Request(), budget.Token);
            Assert.Equal(expected, await PipeFraming.ReceiveReplyAndAcknowledgeAsync(status, budget.Token));
            await servingSecond;
            Assert.False(servingFirst.IsCompleted);
        }
        finally { release.TrySetResult(); }
        Assert.Equal(expected, await PipeFraming.ReceiveReplyAndAcknowledgeAsync(slow, budget.Token));
        await servingFirst;
    }

    [Fact]
    public async Task OwnershipAnchorRejectsASecondFirstInstance()
    {
        string name = "SLX.Tests." + Guid.NewGuid().ToString("N");
        await using var anchor = FixtureServer(name);
        Assert.Throws<UnauthorizedAccessException>(() => FixtureServer(name));
        await using var sibling = FixtureServer(name, firstInstance: false);
        Assert.False(sibling.IsConnected);
    }

    private static PipeRequest Request() => new(PipeContract.Version, "status", Guid.NewGuid());
    private static NamedPipeServerStream FixtureServer(string name, bool firstInstance = true)
    {
        // Production grants server rights only to SYSTEM/admin. Fixtures run without elevation and own only this
        // random private test pipe; WindowsPipeTests separately verifies the unchanged production ACL.
        var acl = new PipeSecurity();
        acl.SetAccessRuleProtection(true, false);
        acl.AddAccessRule(new PipeAccessRule(WindowsIdentity.GetCurrent().User!, PipeAccessRights.FullControl,
            AccessControlType.Allow));
        return ControlPipe.CreateWithSecurity(name, firstInstance, acl);
    }
    private static NamedPipeClientStream Client(string name) => new(".", name, PipeDirection.InOut,
        PipeOptions.Asynchronous, TokenImpersonationLevel.Impersonation);
}
