using NestProxy.Server.Core;
using NestProxy.Server.Streaming.Downstream.Server.Describe;

using Xunit;

namespace NestProxy.Tests.Services;

public class DescribeSynthesizerTests
{
    [Fact]
    public void Build_ReturnsNull_WhenNoInputs()
    {
        var cam = new CameraStream { CameraId = "cam-1", Sdp = null, UpstreamVideoMediaLines = null, UpstreamAudioMediaLines = null };
        var synth = new DescribeSynthesizer(new SdpRewriter());
        Assert.Null(synth.Build(cam));
    }

    [Fact]
    public void Build_CombinesAndNormalizesVideoAndAudio()
    {
        var cam = new CameraStream
        {
            CameraId = "cam-2",
            Sdp =
            [
                "v=0",
                "o=- 0 0 IN IP4 127.0.0.1",
                "s=Test",
                "c=IN IP4 192.168.1.50", // should be neutralized
                "t=0 0"
            ],
            UpstreamVideoMediaLines =
            [
                "m=video 5004 RTP/AVP 96",
                "a=rtpmap:96 H264/90000"
            ],
            UpstreamAudioMediaLines =
            [
                "m=audio 6002 RTP/AVP 97",
                "a=rtpmap:97 OPUS/48000/2"
            ]
        };

        var synth = new DescribeSynthesizer(new SdpRewriter());
        string? sdp = synth.Build(cam);
        Assert.NotNull(sdp);
        // Connection line neutralized
        Assert.Contains("c=IN IP4 0.0.0.0", sdp!);
        // Media ports zeroed
        Assert.Contains("m=video 0 RTP/AVP 96", sdp);
        Assert.Contains("m=audio 0 RTP/AVP 97", sdp);
        // Order: base SDP lines first, then video, then audio (implementation detail we rely on for stability)
        int idxVideo = sdp.IndexOf("m=video", StringComparison.Ordinal);
        int idxAudio = sdp.IndexOf("m=audio", StringComparison.Ordinal);
        Assert.True(idxVideo >= 0 && idxAudio >= 0 && idxVideo < idxAudio);
    }
}