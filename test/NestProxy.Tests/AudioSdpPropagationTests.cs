using NestProxy.Rtsp.Server;
using NestProxy.Server.Core;
using NestProxy.Server.Streaming.Downstream.Server;
using NestProxy.Server.Streaming.Downstream.Server.Describe;

using Xunit;

namespace NestProxy.Tests;

public class AudioSdpPropagationTests
{
    private class DummyListenerManager : IListenerManager
    {
        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void RegisterOnAccepted(Func<RtspServerConnection, Task> onAccepted) { }
    }

    [Fact]
    public void DescribeIncludesAudioMediaSectionWhenPresent()
    {
        // Arrange: build a camera registration with both video and audio media lines
        string[] fullSdp =
        [
            "v=0",
            "o=- 0 0 IN IP4 127.0.0.1",
            "s=NestProxy Test",
            "t=0 0",
            "a=control:*",
            "m=video 9 RTP/AVP 97",
            "c=IN IP4 1.2.3.4",
            "a=rtpmap:97 H264/90000",
            "a=control:trackID=0",
            "m=audio 9 RTP/AVP 96",
            "c=IN IP4 1.2.3.4",
            "a=rtpmap:96 OPUS/48000/2",
            "a=control:trackID=1"
        ];

        CameraStream camera = new()
        {
            CameraId = "camera-1",
            Sdp = fullSdp,
            VideoPayloadType = 97,
            AudioPayloadType = 96,
            UpstreamVideoMediaLines = ["m=video 9 RTP/AVP 97", "c=IN IP4 1.2.3.4", "a=rtpmap:97 H264/90000", "a=control:trackID=0"],
            UpstreamAudioMediaLines = ["m=audio 9 RTP/AVP 96", "c=IN IP4 1.2.3.4", "a=rtpmap:96 OPUS/48000/2", "a=control:trackID=1"]
        };

        var cameras = new CameraRegistry();
        cameras[camera.CameraId] = camera;

        var describeSynthesizer = new DescribeSynthesizer(new SdpRewriter());

        // Act
        string? described = describeSynthesizer.Build(camera);

        // Assert
        Assert.NotNull(described);
        string sdp = described!;
        Assert.Contains("m=video 0 RTP/AVP 97", sdp); // port zeroed
        Assert.Contains("m=audio 0 RTP/AVP 96", sdp); // port zeroed
        Assert.Contains("a=rtpmap:97 H264/90000", sdp);
        Assert.Contains("a=rtpmap:96 OPUS/48000/2", sdp);
        Assert.Contains("a=control:trackID=0", sdp);
        Assert.Contains("a=control:trackID=1", sdp);
    }
}
