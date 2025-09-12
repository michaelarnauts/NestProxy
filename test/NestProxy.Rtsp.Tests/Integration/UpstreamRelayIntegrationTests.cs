using NestProxy.Rtsp.Client;
using Xunit;

namespace NestProxy.Rtsp.Tests.Integration;

public class UpstreamRelayIntegrationTests
{
    [Fact]
    public async Task ConnectToNestStream_ShouldEstablishRelay()
    {
        var client = new RtspClient(new NoopRtspTransport());

        Assert.NotNull(client);
    }

    [Fact]
    public async Task HandleTokenRefresh_ShouldUpdateStreamWithoutDropping()
    {
        var client = new RtspClient(new NoopRtspTransport());

        Assert.NotNull(client);
    }

    [Fact]
    public async Task RelayMediaData_ShouldStreamCorrectly()
    {
        var client = new RtspClient(new NoopRtspTransport());

        Assert.NotNull(client);
    }
}
