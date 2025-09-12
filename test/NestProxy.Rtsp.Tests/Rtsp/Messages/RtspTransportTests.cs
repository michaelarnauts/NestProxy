using NestProxy.Rtsp.Messages;
using Xunit;

namespace NestProxy.Rtsp.Tests.Rtsp.Messages;

public class RtspTransportTests
{
    [Fact]
    public void Constructor_InitializesDefaultValues()
    {
        var transport = new RtspTransport();

        Assert.Equal("TCP", transport.LowerTransport);
        Assert.Null(transport.Interleaved);
        Assert.Null(transport.ClientPort);
        Assert.Null(transport.ServerPort);
        Assert.False(transport.IsMulticast);
    }

    [Fact]
    public void Parse_TcpInterleaved_ParsesCorrectly()
    {
        var header = "RTP/AVP/TCP;interleaved=0-1";

        var transport = RtspTransport.Parse(header);

        Assert.Equal("TCP", transport.LowerTransport);
        Assert.NotNull(transport.Interleaved);
        Assert.Equal(0, transport.Interleaved.Value.First);
        Assert.Equal(1, transport.Interleaved.Value.Second);
    }

    [Fact]
    public void Parse_UdpUnicast_ParsesCorrectly()
    {
        var header = "RTP/AVP;unicast;client_port=8000-8001;server_port=9000-9001";

        var transport = RtspTransport.Parse(header);

        Assert.Equal("UDP", transport.LowerTransport);
        Assert.NotNull(transport.ClientPort);
        Assert.Equal(8000, transport.ClientPort.Value.First);
        Assert.Equal(8001, transport.ClientPort.Value.Second);
        Assert.NotNull(transport.ServerPort);
        Assert.Equal(9000, transport.ServerPort.Value.First);
        Assert.Equal(9001, transport.ServerPort.Value.Second);
        Assert.False(transport.IsMulticast);
    }

    [Fact]
    public void Parse_UdpMulticast_ParsesCorrectly()
    {
        var header = "RTP/AVP;multicast;destination=224.2.0.1;client_port=3456-3457";

        var transport = RtspTransport.Parse(header);

        Assert.Equal("UDP", transport.LowerTransport);
        Assert.Equal("224.2.0.1", transport.Destination);
        Assert.True(transport.IsMulticast);
        Assert.NotNull(transport.ClientPort);
        Assert.Equal(3456, transport.ClientPort.Value.First);
    }

    [Fact]
    public void Parse_WithSsrc_ParsesHexValue()
    {
        var header = "RTP/AVP/TCP;interleaved=0-1;ssrc=DEADBEEF";

        var transport = RtspTransport.Parse(header);

        Assert.NotNull(transport.Ssrc);
        Assert.Equal(0xDEADBEEFu, transport.Ssrc.Value);
    }

    [Fact]
    public void Parse_WithMode_ParsesMode()
    {
        var header = "RTP/AVP;unicast;mode=\"PLAY\"";

        var transport = RtspTransport.Parse(header);

        Assert.Equal("PLAY", transport.Mode);
    }

    [Fact]
    public void Parse_WithSource_ParsesSource()
    {
        var header = "RTP/AVP;unicast;source=192.168.1.1;client_port=8000-8001";

        var transport = RtspTransport.Parse(header);

        Assert.Equal("192.168.1.1", transport.Source);
    }

    [Fact]
    public void Parse_CaseInsensitive_ParsesCorrectly()
    {
        var header = "rtp/avp/tcp;INTERLEAVED=2-3";

        var transport = RtspTransport.Parse(header);

        Assert.Equal("TCP", transport.LowerTransport);
        Assert.NotNull(transport.Interleaved);
        Assert.Equal(2, transport.Interleaved.Value.First);
    }

    [Fact]
    public void ToString_TcpInterleaved_FormatsCorrectly()
    {
        var transport = new RtspTransport
        {
            LowerTransport = "TCP",
            Interleaved = new PortCouple(0, 1)
        };

        var result = transport.ToString();

        Assert.Equal("RTP/AVP/TCP;interleaved=0-1", result);
    }

    [Fact]
    public void ToString_UdpUnicast_FormatsCorrectly()
    {
        var transport = new RtspTransport
        {
            LowerTransport = "UDP",
            ClientPort = new PortCouple(8000, 8001),
            ServerPort = new PortCouple(9000, 9001)
        };

        var result = transport.ToString();

        Assert.Contains("RTP/AVP", result);
        Assert.Contains("unicast", result);
        Assert.Contains("client_port=8000-8001", result);
        Assert.Contains("server_port=9000-9001", result);
    }

    [Fact]
    public void ToString_UdpMulticast_FormatsCorrectly()
    {
        var transport = new RtspTransport
        {
            LowerTransport = "UDP",
            Destination = "224.2.0.1",
            ClientPort = new PortCouple(3456, 3457)
        };

        var result = transport.ToString();

        Assert.Contains("RTP/AVP", result);
        Assert.Contains("multicast", result);
        Assert.Contains("destination=224.2.0.1", result);
        Assert.Contains("client_port=3456-3457", result);
    }

    [Fact]
    public void ToString_WithSsrc_IncludesSsrc()
    {
        var transport = new RtspTransport
        {
            LowerTransport = "TCP",
            Interleaved = new PortCouple(0, 1),
            Ssrc = 0xDEADBEEF
        };

        var result = transport.ToString();

        Assert.Contains("ssrc=DEADBEEF", result);
    }

    [Fact]
    public void ToString_WithMode_IncludesMode()
    {
        var transport = new RtspTransport
        {
            LowerTransport = "UDP",
            Mode = "PLAY"
        };

        var result = transport.ToString();

        Assert.Contains("mode=\"PLAY\"", result);
    }

    [Fact]
    public void Parse_ToString_RoundTrip_PreservesValues()
    {
        var original = "RTP/AVP/TCP;interleaved=0-1;ssrc=12345678";

        var transport = RtspTransport.Parse(original);
        var result = transport.ToString();
        var reparsed = RtspTransport.Parse(result);

        Assert.Equal(transport.LowerTransport, reparsed.LowerTransport);
        Assert.Equal(transport.Interleaved, reparsed.Interleaved);
        Assert.Equal(transport.Ssrc, reparsed.Ssrc);
    }

    [Fact]
    public void IsMulticast_WithDestination_ReturnsTrue()
    {
        var transport = new RtspTransport
        {
            Destination = "224.0.0.1"
        };

        Assert.True(transport.IsMulticast);
    }

    [Fact]
    public void IsMulticast_WithoutDestination_ReturnsFalse()
    {
        var transport = new RtspTransport();

        Assert.False(transport.IsMulticast);
    }
}
