using NestProxy.Server.Streaming.Protocol;
using Xunit;

namespace NestProxy.Tests.Streaming.Protocol;

public class RtcpPacketBuilderTests
{
    [Fact]
    public void ComputeCompoundRrWithSdesLength_ReturnsExpectedLength()
    {
        int cnameLength = 8;
        int len = RtcpPacketBuilder.ComputeCompoundRrWithSdesLength(cnameLength);
        Assert.True(len > cnameLength);
    }

    [Fact]
    public void TryWriteCompoundRrWithSdes_WritesPacket()
    {
        byte[] buffer = new byte[256];
        uint ssrc = 123u;
        byte[] cname = System.Text.Encoding.ASCII.GetBytes("testcname");
        bool ok = RtcpPacketBuilder.TryWriteCompoundRrWithSdes(buffer, ssrc, cname, out int written);
        Assert.True(ok);
        Assert.True(written > 0);
    }

    [Fact]
    public void BuildCompoundRrWithSdes_ReturnsNonEmptyArray()
    {
        byte[] arr = RtcpPacketBuilder.BuildCompoundRrWithSdes(42u, "cname");
        Assert.NotNull(arr);
        Assert.True(arr.Length > 0);
    }

    [Fact]
    public void BuildBye_ReturnsNonEmptyArray()
    {
        byte[] arr = RtcpPacketBuilder.BuildBye(99u);
        Assert.NotNull(arr);
        Assert.True(arr.Length > 0);
    }
}
