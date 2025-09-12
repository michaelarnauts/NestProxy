using NestProxy.Server.Core;
using NestProxy.Server.Streaming.Downstream.Server.Describe;

using Xunit;

namespace NestProxy.Tests.Services;

public class ControlMapBuilderTests
{
    [Fact]
    public void Rebuild_PopulatesMap_ForVideoAndAudio()
    {
        var cam = new CameraStream
        {
            CameraId = "cam-10",
            Sdp =
            [
                "v=0",
                "o=- 0 0 IN IP4 127.0.0.1",
                "s=Test",
                "t=0 0",
                "m=video 9 RTP/AVP 97",
                "a=rtpmap:97 H264/90000",
                "a=control:trackID=0",
                "m=audio 9 RTP/AVP 96",
                "a=rtpmap:96 OPUS/48000/2",
                "a=control:trackID=1"
            ]
        };
        IControlMapBuilder builder = new ControlMapBuilder();
        builder.Rebuild(cam);
        Assert.Equal(2, cam.ControlToPayloadMap.Count);
        Assert.Equal(97, cam.ControlToPayloadMap["trackID=0"]);
        Assert.Equal(96, cam.ControlToPayloadMap["trackID=1"]);
    }

    [Fact]
    public void Rebuild_SafeOnNullSdp()
    {
        var cam = new CameraStream { CameraId = "cam-11", Sdp = null };
        IControlMapBuilder builder = new ControlMapBuilder();
        builder.Rebuild(cam);
        Assert.Empty(cam.ControlToPayloadMap);
    }
}