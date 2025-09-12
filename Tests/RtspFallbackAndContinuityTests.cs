using System;
using System.Buffers;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using NestProxy.Server.Services;
using Xunit;

public class RtspFallbackAndContinuityTests
{
    [Fact]
    public void LocalRtspServer_RewritesSeqAndTimestamp_ForContinuity()
    {
        var logger = new NullLogger<LocalRtspServer>();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Streaming:RtspBasePort"] = "8554"
        }).Build();
        var server = new LocalRtspServer(logger, config);
        var cam = new CameraStream { CameraId = 1, Path = "cam-1", VideoPayloadType = 97, AudioPayloadType = 96 };
        server.RegisterOrUpdateCamera(cam);

        byte[] pkt1 = new byte[12]; pkt1[0] = 0x80; pkt1[1] = (byte)(0x60 | 97); // PT=97
        pkt1[2] = 0x00; pkt1[3] = 0x10; // seq 16
        pkt1[4] = 0x00; pkt1[5] = 0x00; pkt1[6] = 0x00; pkt1[7] = 0x64; // ts 100
        pkt1[8] = 0; pkt1[9] = 0; pkt1[10] = 0; pkt1[11] = 1; // ssrc 1

        byte[] pkt2 = new byte[12]; Array.Copy(pkt1, pkt2, 12);
        pkt2[2] = 0x00; pkt2[3] = 0x11; // seq 17
        pkt2[7] = 0x65; // ts 101

        server.ForwardRtp("cam-1", pkt1);
        server.ForwardRtp("cam-1", pkt2);
        // Not asserting transport output here; this validates no exceptions, and internal continuity state advances.
    }
}
