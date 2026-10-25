using System.Net;
using System.Net.NetworkInformation;
using Slx.Client.Windows;
using Xunit;

namespace Slx.Client.Tests;

public sealed class PublicIpv6SourceTests
{
    [Theory]
    [InlineData("2001:4860:4860::8844", true, DuplicateAddressDetectionState.Preferred, true)]
    [InlineData("2001:4860:4860::8844", false, DuplicateAddressDetectionState.Preferred, false)]
    [InlineData("2001:4860:4860::8844", true, DuplicateAddressDetectionState.Tentative, false)]
    [InlineData("2001:4860:4860::8844", true, DuplicateAddressDetectionState.Deprecated, false)]
    [InlineData("2001:4860:4860::8844", true, DuplicateAddressDetectionState.Duplicate, false)]
    [InlineData("fd73:6c78::1", true, DuplicateAddressDetectionState.Preferred, false)]
    [InlineData("fe80::1", true, DuplicateAddressDetectionState.Preferred, false)]
    [InlineData("ff02::1", true, DuplicateAddressDetectionState.Preferred, false)]
    [InlineData("::", true, DuplicateAddressDetectionState.Preferred, false)]
    [InlineData("::1", true, DuplicateAddressDetectionState.Preferred, false)]
    [InlineData("::ffff:8.8.8.8", true, DuplicateAddressDetectionState.Preferred, false)]
    [InlineData("8.8.8.8", true, DuplicateAddressDetectionState.Preferred, false)]
    public void OnlyUpPreferredPublicIpv6SourcesRequireAnIpv6RouteRead(string address, bool up,
        DuplicateAddressDetectionState state, bool expected)
        => Assert.Equal(expected, PublicIpv6Source.IsAvailable([new(IPAddress.Parse(address), up, state)]));

    [Fact]
    public void LocalTunnelAndIpv4OnlyInterfacesDoNotPretendGlobalIpv6Exists()
    {
        Assert.False(PublicIpv6Source.IsAvailable([]));
        Assert.False(PublicIpv6Source.IsAvailable([
            new(IPAddress.Parse("fd73:6c78::1"), true, DuplicateAddressDetectionState.Preferred),
            new(IPAddress.Parse("192.168.1.2"), true, DuplicateAddressDetectionState.Preferred)]));
    }
}
