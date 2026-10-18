using System.Net;
using Slx.Client.Core;
using Xunit;

namespace Slx.Client.Tests;

public sealed class CorePlanTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("0.0.0.0")]
    [InlineData("169.254.169.254")]
    [InlineData("192.168.1.1")]
    [InlineData("100.64.1.1")]
    [InlineData("198.18.1.1")]
    [InlineData("203.0.113.1")]
    [InlineData("::1")]
    [InlineData("fe80::1")]
    [InlineData("fc00::1")]
    [InlineData("::ffff:127.0.0.1")]
    [InlineData("2001:db8::1")]
    [InlineData("2002:7f00:1::")]
    [InlineData("2001:20::1")]
    [InlineData("3fff::1")]
    [InlineData("2606:4700:4700::1111%3")]
    public void RejectsNonPublicRemoteAddresses(string value) =>
        Assert.False(EndpointPolicy.IsPublicAddress(IPAddress.Parse(value)));

    [Theory]
    [InlineData("1.1.1.1")]
    [InlineData("8.8.8.8")]
    [InlineData("2606:4700:4700::1111")]
    public void AcceptsPublicRemoteAddresses(string value) =>
        Assert.True(EndpointPolicy.IsPublicAddress(IPAddress.Parse(value)));

    [Theory]
    [InlineData("browser.exe")]
    [InlineData(@"\\server\share\browser.exe")]
    [InlineData(@"C:\Apps\..\Windows\browser.exe")]
    [InlineData(@"C:\Apps\browser.exe:stream")]
    [InlineData(@"C:\Apps\*.exe")]
    [InlineData(@"C:\Apps\browser.cmd")]
    public void RejectsApplicationPatternsAndUntrustedPathSyntax(string value)
    {
        var plan = CoreFixtures.Plan() with { Applications = [CoreFixtures.App(value)] };
        Assert.Equal("INVALID_APPLICATION", Assert.Throws<ClientFault>(() => PlanValidator.Validate(plan)).Code);
    }

    [Fact]
    public void RequiresAnEnabledApplicationOnlyForSelectedMode()
    {
        var plan = CoreFixtures.Plan() with { Applications = [] };
        Assert.Equal("INVALID_APPLICATION", Assert.Throws<ClientFault>(() => PlanValidator.Validate(plan)).Code);
        PlanValidator.Validate(plan with { Mode = RoutingMode.WholeComputer });
    }

    [Fact]
    public void DoesNotPermitDuplicatePathsOrClientInjectedTransport()
    {
        var plan = CoreFixtures.Plan();
        Assert.Throws<ClientFault>(() => PlanValidator.Validate(plan with
        {
            Applications = [CoreFixtures.App(), CoreFixtures.App(@"c:\Apps\Browser\browser.exe")]
        }));
        Assert.Throws<ClientFault>(() => PlanValidator.Validate(plan with
        {
            Profile = plan.Profile with { Transport = "freedom" }
        }));
        Assert.Throws<ClientFault>(() => PlanValidator.Validate(plan with { Mode = (RoutingMode)999 }));
    }
}
