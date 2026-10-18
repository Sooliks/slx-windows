using System.Net.NetworkInformation;
using Slx.Client.Service;
using Xunit;
namespace Slx.Client.Tests;
public sealed class PresentAdaptersTests
{
    [Fact]
    public void NativePresentDeviceQueryFindsThePhysicalInterfaceButNotAnAbsentGuid()
    {
        var physical = NetworkInterface.GetAllNetworkInterfaces().First(adapter =>
            adapter.OperationalStatus == OperationalStatus.Up && adapter.NetworkInterfaceType is NetworkInterfaceType.Ethernet or NetworkInterfaceType.Wireless80211);
        Assert.True(PresentAdapters.Contains(physical.Id));
        Assert.False(PresentAdapters.Contains(Guid.NewGuid().ToString()));
    }
    [Fact]
    public void InvalidIdentityFailsClosed()
    {
        Assert.Throws<Slx.Client.Core.ClientFault>(() => PresentAdapters.Contains("not-an-interface-guid"));
    }
}
