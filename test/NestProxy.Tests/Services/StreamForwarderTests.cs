using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NestProxy.Rtsp.Server;
using NestProxy.Server.Core;
using NestProxy.Server.Models;
using NestProxy.Server.Streaming.Downstream.Sessions;
using NestProxy.Server.Streaming.Forwarding;
using NestProxy.Server.Streaming.Forwarding.Binding;

using Xunit;

namespace NestProxy.Tests.Services;

public class StreamForwarderTests
{
    private static (StreamForwarder forwarder, CameraStream cam, ClientSession session, StatisticsReporter stats) BuildHarness()
    {
        var cameraRegistry = new CameraRegistry();
        var cam = new CameraStream
        {
            CameraId = "cam-42",
            Sdp = ["v=0", "o=- 0 0 IN IP4 1.1.1.1", "s=Test", "t=0 0"],
            VideoPayloadType = 97
        };
        cameraRegistry[cam.CameraId] = cam;
        var selector = new BindingSelector();
        var options = Options.Create(new StreamingOptions { StatsWindowSeconds = 1 });
        var stats = new StatisticsReporter(cameraRegistry, options, NullLogger<StatisticsReporter>.Instance);
        var sessionManager = new SessionRegistry(cameraRegistry, options, NullLogger<SessionRegistry>.Instance);
        var forwarder = new StreamForwarder(cameraRegistry, sessionManager, selector, stats, new RtpPacketParser(), NullLogger<StreamForwarder>.Instance);
        // Create session via registry API for consistency
        var dummyConnection = new RtspServerConnection(new SessionManagerTests.FakeRtspTransportAccessor().Transport);
        ClientSession session = sessionManager.GetOrCreateSession(cam, dummyConnection);
        // Mark session as playing to allow forwarding logic (which gates on IsPlaying)
        session.IsPlaying = true;
        var binding = new TrackBinding();
        session.BindingsByPayload[cam.VideoPayloadType] = [binding];
        return (forwarder, cam, session, stats);
    }

    [Fact]
    public async Task ForwardRtp_AssociatesSsrcOnFirstPacket()
    {
        var (forwarder, cam, session, stats) = BuildHarness();
        byte[] rtp = BuildRtpPacket(ssrc: 0x01020304, payloadType: cam.VideoPayloadType, seq: 1, ts: 90000);

        await forwarder.ForwardRtpAsync(cam.CameraId, rtp);

        // Force log flush (not strictly needed) then inspect snapshot
        var snapshot1 = stats.TrySnapshot(cam.CameraId, session.SessionId) as IEnumerable<object>;
        int rtp1 = ExtractRtp(snapshot1!, cam.VideoPayloadType);
        Assert.Equal(1, rtp1);
    }

    [Fact]
    public async Task ForwardRtp_UsesExistingAssociationOnSecondPacket()
    {
        var (forwarder, cam, session, stats) = BuildHarness();
        byte[] first = BuildRtpPacket(0x0A0B0C0Du, cam.VideoPayloadType, 10, 1234);
        byte[] second = BuildRtpPacket(0x0A0B0C0Du, cam.VideoPayloadType, 11, 1234 + 3600);

        await forwarder.ForwardRtpAsync(cam.CameraId, first);
        await forwarder.ForwardRtpAsync(cam.CameraId, second);

        var snapshot2 = stats.TrySnapshot(cam.CameraId, session.SessionId) as IEnumerable<object>;
        int rtp2 = ExtractRtp(snapshot2!, cam.VideoPayloadType);
        Assert.Equal(2, rtp2);
    }

    private static int ExtractRtp(IEnumerable<object> snapshot, int payloadType)
    {
        foreach (object entry in snapshot)
        {
            var ptProp = entry.GetType().GetProperty("payloadType");
            var rtpProp = entry.GetType().GetProperty("rtp");
            if (ptProp != null && rtpProp != null && (int)ptProp.GetValue(entry)! == payloadType)
            {
                return (int)(long)rtpProp.GetValue(entry)!;
            }
        }

        return 0;
    }

    private static byte[] BuildRtpPacket(uint ssrc, int payloadType, ushort seq, uint ts)
    {
        byte[] buffer =
        [
            0x80, // V=2
            (byte)(payloadType & 0x7F),
            (byte)(seq >> 8),
            (byte)(seq & 0xFF),
            (byte)(ts >> 24),
            (byte)(ts >> 16),
            (byte)(ts >> 8),
            (byte)(ts & 0xFF),
            (byte)(ssrc >> 24),
            (byte)(ssrc >> 16),
            (byte)(ssrc >> 8),
            (byte)(ssrc & 0xFF),
        ];
        return buffer;
    }
}
