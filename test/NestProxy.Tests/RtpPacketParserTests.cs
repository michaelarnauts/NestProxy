using System;
using Xunit;
using NestProxy.Server.Streaming.Forwarding;

namespace NestProxy.Tests;

public class RtpPacketParserTests
{
    private readonly RtpPacketParser _parser = new();

    [Fact]
    public void IsValidRtp_ReturnsTrue_ForValidPacket()
    {
        var packet = new byte[12];
        Assert.True(_parser.IsValidRtp(packet));
    }

    [Fact]
    public void IsValidRtp_ReturnsFalse_ForShortPacket()
    {
        var packet = new byte[8];
        Assert.False(_parser.IsValidRtp(packet));
    }

    [Fact]
    public void IsValidRtcp_ReturnsTrue_ForValidPacket()
    {
        var packet = new byte[8];
        Assert.True(_parser.IsValidRtcp(packet));
    }

    [Fact]
    public void IsValidRtcp_ReturnsFalse_ForShortPacket()
    {
        var packet = new byte[4];
        Assert.False(_parser.IsValidRtcp(packet));
    }

    [Fact]
    public void GetSsrc_ExtractsSsrc_ForRtp()
    {
        var packet = new byte[12];
        packet[8] = 0x01;
        packet[9] = 0x02;
        packet[10] = 0x03;
        packet[11] = 0x04;
        var ssrc = _parser.GetSsrc(packet, true);
        Assert.Equal(0x01020304u, ssrc);
    }

    [Fact]
    public void GetSsrc_ExtractsSsrc_ForRtcp()
    {
        var packet = new byte[8];
        packet[4] = 0x05;
        packet[5] = 0x06;
        packet[6] = 0x07;
        packet[7] = 0x08;
        var ssrc = _parser.GetSsrc(packet, false);
        Assert.Equal(0x05060708u, ssrc);
    }

    [Fact]
    public void GetPayloadType_ExtractsPayloadType()
    {
        var packet = new byte[12];
        packet[1] = 0x7F;
        var pt = _parser.GetPayloadType(packet);
        Assert.Equal(0x7F, pt);
    }

    [Fact]
    public void GetRtcpPacketType_ExtractsPacketType()
    {
        var packet = new byte[8];
        packet[1] = 0xC8;
        var type = _parser.GetRtcpPacketType(packet);
        Assert.Equal((byte)0xC8, type);
    }
}
