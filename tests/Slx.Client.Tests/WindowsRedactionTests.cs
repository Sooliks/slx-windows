using System.Text.Json;
using Slx.Client.Core;
using Xunit;

namespace Slx.Client.Tests;

public sealed class WindowsRedactionTests
{
    [Fact]
    public void RecordDiagnosticsCannotExposeCredentialsOrPersonalUrls()
    {
        var profile = CoreFixtures.Profile();
        var snapshot = SubscriptionParser.Parse(CoreFixtures.Hysteria);
        var stored = new StoredSubscription(WindowsSubscriptionTests.Url, snapshot);
        var plan = CoreFixtures.Plan();
        foreach (var value in new object[] { profile, snapshot, stored, plan, plan.Applications[0], new ClientPreferences(Applications: plan.Applications) })
        {
            var diagnostic = value.ToString()!;
            Assert.DoesNotContain(CoreFixtures.Token, diagnostic);
            Assert.DoesNotContain(WindowsSubscriptionTests.Url, diagnostic);
            Assert.DoesNotContain(plan.Applications[0].ExecutablePath, diagnostic);
            Assert.Contains("[redacted]", diagnostic);
        }
        // Redaction is diagnostic only; typed IPC and DPAPI persistence still carry the actual credential.
        Assert.Contains(CoreFixtures.Token, JsonSerializer.Serialize(plan));
        Assert.Contains(WindowsSubscriptionTests.Url, JsonSerializer.Serialize(stored));
    }
}
