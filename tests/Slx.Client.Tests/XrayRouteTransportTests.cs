using System.Net;
using System.Net.Http.Headers;
using Slx.Client.Core;
using Slx.Client.Windows;
using Xunit;
namespace Slx.Client.Tests;
public sealed class XrayRouteTransportTests
{
    [Theory]
    [InlineData("status")]
    [InlineData("missing-status")]
    [InlineData("duplicate-status")]
    [InlineData("version")]
    [InlineData("http")]
    [InlineData("type")]
    [InlineData("compression")]
    [InlineData("length")]
    [InlineData("extra-message")]
    [InlineData("oversize")]
    public async Task InvalidGrpcNeverConfirmsAMutation(string failure)
    {
        var body = failure switch
        {
            "compression" => new byte[] { 1, 0, 0, 0, 0 },
            "length" => new byte[] { 0, 0, 0, 0, 1 },
            "extra-message" => new byte[10], "oversize" => new byte[65537], _ => new byte[5]
        };
        using var response = Response(body);
        if (failure == "status") { response.TrailingHeaders.Remove("grpc-status"); response.TrailingHeaders.Add("grpc-status", "13"); }
        if (failure == "missing-status") response.TrailingHeaders.Remove("grpc-status");
        if (failure == "duplicate-status") response.Headers.Add("grpc-status", "0");
        if (failure == "version") response.Version = HttpVersion.Version11;
        if (failure == "http") response.StatusCode = HttpStatusCode.BadGateway;
        if (failure == "type") response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        var fault = await Assert.ThrowsAsync<ClientFault>(() => RouteGrpcMessage.ReadAsync(response, default));
        Assert.Equal("ROUTING_INVALID_REPLY", fault.Code);
    }
    [Fact]
    public async Task EmptySuccessStillRequiresTheCompleteGrpcEnvelope()
    {
        using var response = Response(new byte[5]);
        Assert.Empty(await RouteGrpcMessage.ReadAsync(response, default));
        Assert.Equal(new byte[] { 0, 0, 0, 0, 2, 3, 4 }, RouteGrpcMessage.Frame([3, 4]));
    }
    [Fact]
    public async Task MutationContentCannotBeSerializedAgainByAnInternalHttpRetry()
    {
        using var content = RouteSingleUseContent.Create([3, 4]); using var first = new MemoryStream(); using var replay = new MemoryStream();
        await content.CopyToAsync(first);
        await Assert.ThrowsAsync<ClientFault>(() => content.CopyToAsync(replay)); Assert.Equal(0, replay.Length);
    }
    [Theory]
    [InlineData(80, "probeUser00000000", "probePassword0000")]
    [InlineData(18080, "bad:user000000000", "probePassword0000")]
    [InlineData(18080, "probeUser00000000", "password\r\nsecret")]
    public void InvalidTransportCredentialsNeverReachTheNetwork(int port, string user, string password)
        => Assert.Throws<ClientFault>(() => new XrayRouteControl(port, user, password));
    [Theory]
    [InlineData(new byte[] { 0 })]
    [InlineData(new byte[] { 10, 127 })]
    [InlineData(new byte[] { 10, 1, 0 })]
    [InlineData(new byte[] { 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 2 })]
    public void MalformedRevisionMessagesNeverLookApplied(byte[] response)
        => Assert.Throws<ClientFault>(() => XrayRouteControl.ReadRevision(response));
    private static HttpResponseMessage Response(byte[] bytes)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Version = HttpVersion.Version20, Content = new ByteArrayContent(bytes) };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/grpc+proto"); response.TrailingHeaders.Add("grpc-status", "0"); return response;
    }
}
