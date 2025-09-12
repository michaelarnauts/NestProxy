using NestProxy.Server.Core;
using NestProxy.Server.Streaming.Downstream.Server;
using NestProxy.Server.Streaming.Protocol;

using Xunit;

namespace NestProxy.Tests;

public class RtcpAndAuthTests
{
    private class FakeLocalServer : ILocalRtspServer
    {
        public void RegisterOrUpdateCamera(CameraStream stream) { }
        public void ForwardRtp(string path, ReadOnlyMemory<byte> rtpPacket) { }
        public ValueTask ForwardRtpAsync(string cameraPath, ReadOnlyMemory<byte> rtpPacket, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public void ForwardRtcp(string path, ReadOnlyMemory<byte> rtcpPacket) { }
        public ValueTask ForwardRtcpAsync(string cameraPath, ReadOnlyMemory<byte> rtcpPacket, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public string BuildPublicUrl(string host, int port, string path) => $"rtsp://{host}:{port}/{path}";
        public string? DescribeForTest(string cameraPath) => null;
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [Fact]
    public void BuildCompoundRtcpRrPacket_HasValidRrAndSdes()
    {
        // Arrange: build a deterministic packet
        uint ssrc = 0x10203040;
        string cnameExpected = "cam-42@nestproxy";
        byte[] pkt = RtcpPacketBuilder.BuildCompoundRrWithSdes(ssrc, cnameExpected);
        Assert.True(pkt.Length >= 8);
        byte vprc = pkt[0];
        byte pt = pkt[1];
        ushort lenWordsMinus1 = (ushort)((pkt[2] << 8) | pkt[3]);
        Assert.Equal(0x80, vprc);
        Assert.Equal(201, pt);
        Assert.Equal((ushort)1, lenWordsMinus1);
        uint rrSsrc = (uint)(pkt[4] << 24 | pkt[5] << 16 | pkt[6] << 8 | pkt[7]);
        Assert.Equal(ssrc, rrSsrc);
        int sdesOffset = 8;
        Assert.True(pkt.Length >= sdesOffset + 4);
        byte sdesFirst = pkt[sdesOffset + 0];
        byte sdesPt = pkt[sdesOffset + 1];
        ushort sdesLenWordsMinus1 = (ushort)((pkt[sdesOffset + 2] << 8) | pkt[sdesOffset + 3]);
        Assert.Equal(0x81, sdesFirst);
        Assert.Equal(202, sdesPt);
        int sdesTotalLen = (sdesLenWordsMinus1 + 1) * 4;
        Assert.True(pkt.Length >= sdesOffset + sdesTotalLen);
        uint sdesSsrc = (uint)(pkt[sdesOffset + 4] << 24 | pkt[sdesOffset + 5] << 16 | pkt[sdesOffset + 6] << 8 | pkt[sdesOffset + 7]);
        Assert.Equal(ssrc, sdesSsrc);
        int idx = sdesOffset + 8;
        Assert.Equal(0x01, pkt[idx]);
        idx++;
        int cnameLen = pkt[idx];
        idx++;
        Assert.True(cnameLen > 0);
        string cname = System.Text.Encoding.ASCII.GetString(pkt, idx, cnameLen);
        Assert.Equal(cnameExpected, cname);
    }

    [Theory]
    [InlineData("rtsp://x/y", "token", "rtsp://x/y?auth=token")]
    [InlineData("rtsp://x/y?foo=1", "t", "rtsp://x/y?foo=1&auth=t")]
    [InlineData("rtsp://x/y?auth=old", "new", "rtsp://x/y?auth=new")]
    [InlineData("rtsp://x/y?AUTH=old&z=9", "n", "rtsp://x/y?AUTH=n&z=9")]
    public void ReplaceAuthParam_UpdatesOrAdds(string url, string auth, string expected)
    {
        var updated = AuthQueryUpdater.UpdateAuth(url, auth);
        if (expected.Contains('&') && updated.Contains('&'))
        {
            string[] expParts = [.. expected.Split('?')[1].Split('&').OrderBy(x => x)];
            string[] updParts = [.. updated.Split('?')[1].Split('&').OrderBy(x => x)];
            Assert.Equal(expParts, updParts);
            Assert.Equal(expected.Split('?')[0], updated.Split('?')[0]);
        }
        else
        {
            Assert.Equal(expected, updated);
        }
    }
}