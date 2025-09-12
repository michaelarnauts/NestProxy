using NestProxy.Rtsp.Server;
using NestProxy.Server.Core;
using NestProxy.Server.Streaming.Downstream.Server;
using NestProxy.Server.Streaming.Forwarding.Binding;

using Xunit;

namespace NestProxy.Tests;

public class MultiTrackBindingTests
{
    private class DummyListenerManager : IListenerManager
    {
        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void RegisterOnAccepted(Func<RtspServerConnection, Task> onAccepted) { }
    }

    [Fact]
    public void VideoAndAudioProduceDistinctBindingsAfterAssociation()
    {
        // Arrange a camera with video+audio payload types
        CameraStream camera = new()
        {
            CameraId = "multi-av",
            Sdp = [
                "v=0","o=- 0 0 IN IP4 127.0.0.1","s=Test","t=0 0","a=control:*",
                "m=video 9 RTP/AVP 97","c=IN IP4 10.0.0.1","a=rtpmap:97 H264/90000","a=control:trackID=0",
                "m=audio 9 RTP/AVP 96","c=IN IP4 10.0.0.1","a=rtpmap:96 OPUS/48000/2","a=control:trackID=1"
            ],
            UpstreamVideoMediaLines = ["m=video 9 RTP/AVP 97", "c=IN IP4 10.0.0.1", "a=rtpmap:97 H264/90000", "a=control:trackID=0"],
            UpstreamAudioMediaLines = ["m=audio 9 RTP/AVP 96", "c=IN IP4 10.0.0.1", "a=rtpmap:96 OPUS/48000/2", "a=control:trackID=1"],
            VideoPayloadType = 97,
            AudioPayloadType = 96
        };

        var cameras = new CameraRegistry();
        cameras[camera.CameraId] = camera;

        var selector = new BindingSelector();

        // Simulate a client session with two track bindings (video + audio)
        // Minimal listener requirement for ClientSession (not used functionally in this test)
        var fakeTransport = new SessionManagerTests.FakeRtspTransportAccessor();
        RtspServerConnection dummyConnection = new RtspServerConnection(fakeTransport.Transport);
        ClientSession client = new ClientSession
        {
            Connection = dummyConnection
        };

        TrackBinding videoBinding = new();
        TrackBinding audioBinding = new();
        client.BindingsByPayload[camera.VideoPayloadType] = [videoBinding];
        client.BindingsByPayload[camera.AudioPayloadType] = [audioBinding];

        // Act: associate SSRCs (bootstrapped by explicit association rather than automatic due to single binding per payload)
        uint videoSsrc = 1111u;
        uint audioSsrc = 2222u;
        selector.AssociateSsrc(camera.CameraId, client.SessionId, videoSsrc, videoBinding, camera.VideoPayloadType);
        selector.AssociateSsrc(camera.CameraId, client.SessionId, audioSsrc, audioBinding, camera.AudioPayloadType);

        TrackBinding? resolvedVideo = selector.GetOrChooseBinding(camera.CameraId, client.SessionId, videoSsrc, client.BindingsByPayload[camera.VideoPayloadType]);
        TrackBinding? resolvedAudio = selector.GetOrChooseBinding(camera.CameraId, client.SessionId, audioSsrc, client.BindingsByPayload[camera.AudioPayloadType]);

        // Assert
        Assert.Same(videoBinding, resolvedVideo);
        Assert.Same(audioBinding, resolvedAudio);
        Assert.NotSame(resolvedVideo, resolvedAudio);
    }
}
