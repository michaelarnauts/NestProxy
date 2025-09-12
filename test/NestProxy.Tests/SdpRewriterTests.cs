using System;
using Xunit;
using NestProxy.Server.Streaming.Downstream.Server.Describe;

namespace NestProxy.Tests;

public class SdpRewriterTests
{
    private readonly SdpRewriter _rewriter = new();

    [Fact]
    public void Rewrite_ReturnsInput_WhenNoRewriteRequested()
    {
        string[] sdp = ["v=0", "o=- 0 0 IN IP4 127.0.0.1", "s=Test", "t=0 0"];
        string result = _rewriter.Rewrite(sdp, false, false);
        Assert.Contains("v=0", result);
        Assert.Contains("o=- 0 0 IN IP4 127.0.0.1", result);
    }

    [Fact]
    public void Rewrite_RewritesVideoMediaLines()
    {
        string[] video = ["m=video 9 RTP/AVP 97", "c=IN IP4 1.2.3.4", "a=rtpmap:97 H264/90000", "a=control:trackID=0"];
        string result = _rewriter.Rewrite(video, true, false);
        Assert.Contains("m=video 0 RTP/AVP 97", result); // port zeroed
        Assert.Contains("a=control:trackID=0", result);
    }

    [Fact]
    public void Rewrite_RewritesAudioMediaLines()
    {
        string[] audio = ["m=audio 9 RTP/AVP 96", "c=IN IP4 1.2.3.4", "a=rtpmap:96 OPUS/48000/2", "a=control:trackID=1"];
        string result = _rewriter.Rewrite(audio, false, true);
        Assert.Contains("m=audio 0 RTP/AVP 96", result); // port zeroed
        Assert.Contains("a=control:trackID=1", result);
    }
}
