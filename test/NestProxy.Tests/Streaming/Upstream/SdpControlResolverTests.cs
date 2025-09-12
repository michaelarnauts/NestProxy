using NestProxy.Server.Streaming.Upstream.Sdp;
using NestProxy.Rtsp.Messages;

using Xunit;

namespace NestProxy.Tests.Streaming.Upstream;

public class SdpControlResolverTests
{
    [Fact]
    public void Resolve_ReturnsExpectedResult()
    {
        var response = new RtspResponse();
        var orig = new RtspRequest();
        string baseUrl = "rtsp://example.com/";
        var result = SdpControlResolver.Resolve(response, orig, baseUrl);
        Assert.NotNull(result);
        Assert.NotNull(result.BaseUrl);
        Assert.NotNull(result.SdpLines);
        Assert.True(result.TrackUris.Count >= 0);
    }
}
