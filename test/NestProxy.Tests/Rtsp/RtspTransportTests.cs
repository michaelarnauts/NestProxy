using NestProxy.Rtsp.Messages;
using Xunit;

namespace NestProxy.Tests.Rtsp;

/// <summary>
/// Tests for RTSP transport header parsing and negotiation per RFC 2326 Section 12.39.
/// </summary>
public class RtspTransportTests
{
    [Fact]
    public void RtspTransport_ParseTcpInterleaved_ParsesCorrectly()
    {
        // Arrange
        var transportHeader = "RTP/AVP/TCP;unicast;interleaved=0-1";

        // Act
        var transport = RtspTransport.Parse(transportHeader);

        // Assert
        Assert.Equal("TCP", transport.LowerTransport);
        Assert.NotNull(transport.Interleaved);
        Assert.Equal(0, transport.Interleaved?.First);
        Assert.Equal(1, transport.Interleaved?.Second);
    }

    [Fact]
    public void RtspTransport_ParseUdpWithClientPorts_ParsesCorrectly()
    {
        // Arrange
        var transportHeader = "RTP/AVP;unicast;client_port=50000-50001";

        // Act
        var transport = RtspTransport.Parse(transportHeader);

        // Assert
        Assert.Equal("UDP", transport.LowerTransport);
        Assert.Null(transport.Interleaved);
        Assert.Equal(50000, transport.ClientPort?.First);
        Assert.Equal(50001, transport.ClientPort?.Second);
    }

    [Fact]
    public void RtspTransport_ParseUdpWithServerPorts_ParsesCorrectly()
    {
        // Arrange
        var transportHeader = "RTP/AVP;unicast;client_port=50000-50001;server_port=60000-60001";

        // Act
        var transport = RtspTransport.Parse(transportHeader);

        // Assert
        Assert.Equal("UDP", transport.LowerTransport);
        Assert.Equal(50000, transport.ClientPort?.First);
        Assert.Equal(50001, transport.ClientPort?.Second);
        Assert.Equal(60000, transport.ServerPort?.First);
        Assert.Equal(60001, transport.ServerPort?.Second);
    }

    [Fact]
    public void RtspTransport_ParseMulticast_ParsesCorrectly()
    {
        // Arrange
        var transportHeader = "RTP/AVP;multicast;destination=224.2.0.1";

        // Act
        var transport = RtspTransport.Parse(transportHeader);

        // Assert
        Assert.Equal("UDP", transport.LowerTransport);
        Assert.True(transport.IsMulticast);
        Assert.Equal("224.2.0.1", transport.Destination);
    }

    [Fact]
    public void RtspTransport_ParseWithMode_ParsesCorrectly()
    {
        // Arrange
        var transportHeader = "RTP/AVP;unicast;client_port=50000-50001;mode=\"PLAY\"";

        // Act
        var transport = RtspTransport.Parse(transportHeader);

        // Assert
        Assert.Equal("PLAY", transport.Mode);
    }

    [Fact]
    public void RtspTransport_ParseWithSsrc_ParsesCorrectly()
    {
        // Arrange
        var transportHeader = "RTP/AVP/TCP;unicast;interleaved=0-1;ssrc=ABCD1234";

        // Act
        var transport = RtspTransport.Parse(transportHeader);

        // Assert
        Assert.NotNull(transport.Ssrc);
        Assert.Equal(0xABCD1234u, transport.Ssrc);
    }

    [Fact]
    public void RtspTransport_ToString_TcpInterleaved_FormatsCorrectly()
    {
        // Arrange
        var transport = new RtspTransport
        {
            LowerTransport = "TCP",
            Interleaved = new PortCouple(0, 1)
        };

        // Act
        var result = transport.ToString();

        // Assert
        Assert.Contains("RTP/AVP/TCP", result);
        Assert.Contains("interleaved=0-1", result);
    }

    [Fact]
    public void RtspTransport_ToString_UdpWithPorts_FormatsCorrectly()
    {
        // Arrange
        var transport = new RtspTransport
        {
            LowerTransport = "UDP",
            ClientPort = new PortCouple(50000, 50001),
            ServerPort = new PortCouple(60000, 60001)
        };

        // Act
        var result = transport.ToString();

        // Assert
        Assert.Contains("RTP/AVP", result);
        Assert.Contains("unicast", result);
        Assert.Contains("client_port=50000-50001", result);
        Assert.Contains("server_port=60000-60001", result);
    }

    [Fact]
    public void RtspTransport_ToString_Multicast_FormatsCorrectly()
    {
        // Arrange
        var transport = new RtspTransport
        {
            LowerTransport = "UDP",
            Destination = "224.2.0.1"
        };

        // Act
        var result = transport.ToString();

        // Assert
        Assert.Contains("RTP/AVP", result);
        Assert.Contains("multicast", result);
        Assert.Contains("destination=224.2.0.1", result);
    }

    [Fact]
    public void RtspTransport_RoundTrip_TcpInterleaved_PreservesData()
    {
        // Arrange
        var original = "RTP/AVP/TCP;unicast;interleaved=2-3";

        // Act
        var transport = RtspTransport.Parse(original);
        var result = transport.ToString();
        var reparsed = RtspTransport.Parse(result);

        // Assert
        Assert.Equal(transport.LowerTransport, reparsed.LowerTransport);
        Assert.Equal(transport.Interleaved?.First, reparsed.Interleaved?.First);
        Assert.Equal(transport.Interleaved?.Second, reparsed.Interleaved?.Second);
    }

    [Fact]
    public void RtspTransport_RoundTrip_UdpWithPorts_PreservesData()
    {
        // Arrange
        var original = "RTP/AVP;unicast;client_port=50000-50001;server_port=60000-60001";

        // Act
        var transport = RtspTransport.Parse(original);
        var result = transport.ToString();
        var reparsed = RtspTransport.Parse(result);

        // Assert
        Assert.Equal(transport.LowerTransport, reparsed.LowerTransport);
        Assert.Equal(transport.ClientPort?.First, reparsed.ClientPort?.First);
        Assert.Equal(transport.ClientPort?.Second, reparsed.ClientPort?.Second);
        Assert.Equal(transport.ServerPort?.First, reparsed.ServerPort?.First);
        Assert.Equal(transport.ServerPort?.Second, reparsed.ServerPort?.Second);
    }

    [Fact]
    public void PortCouple_Create_StoresCorrectly()
    {
        // Arrange & Act
        var ports = new PortCouple(5000, 5001);

        // Assert
        Assert.Equal(5000, ports.First);
        Assert.Equal(5001, ports.Second);
    }

    [Fact]
    public void PortCouple_ToString_FormatsCorrectly()
    {
        // Arrange
        var ports = new PortCouple(8000, 8001);

        // Act
        var result = ports.ToString();

        // Assert
        Assert.Equal("8000-8001", result);
    }

    [Theory]
    [InlineData("RTP/AVP/TCP;unicast;interleaved=0-1", "TCP", 0, 1)]
    [InlineData("RTP/AVP/TCP;unicast;interleaved=2-3", "TCP", 2, 3)]
    [InlineData("RTP/AVP/TCP;unicast;interleaved=4-5", "TCP", 4, 5)]
    public void RtspTransport_ParseMultipleTcpVariants_ParsesCorrectly(
        string header, string expectedProtocol, int expectedStart, int expectedEnd)
    {
        // Act
        var transport = RtspTransport.Parse(header);

        // Assert
        Assert.Equal(expectedProtocol, transport.LowerTransport);
        Assert.Equal(expectedStart, transport.Interleaved?.First);
        Assert.Equal(expectedEnd, transport.Interleaved?.Second);
    }

    [Theory]
    [InlineData("RTP/AVP;unicast;client_port=50000-50001")]
    [InlineData("RTP/AVP;unicast;client_port=60000-60001")]
    [InlineData("RTP/AVP;unicast;client_port=8000-8001;server_port=9000-9001")]
    public void RtspTransport_ParseMultipleUdpVariants_ParsesCorrectly(string header)
    {
        // Act
        var transport = RtspTransport.Parse(header);

        // Assert
        Assert.Equal("UDP", transport.LowerTransport);
        Assert.NotNull(transport.ClientPort);
    }

    [Fact]
    public void RtspTransport_ParseInvalidFormat_ReturnsDefaultTransport()
    {
        // Arrange
        var invalidHeader = "INVALID_FORMAT";

        // Act
        var transport = RtspTransport.Parse(invalidHeader);

        // Assert - Parse doesn't throw, it returns a default transport
        Assert.NotNull(transport);
    }

    [Fact]
    public void RtspTransport_ParseEmpty_ReturnsDefaultTransport()
    {
        // Arrange
        var emptyHeader = "";

        // Act
        var transport = RtspTransport.Parse(emptyHeader);

        // Assert - Parse doesn't throw, it returns a default transport
        Assert.NotNull(transport);
    }
}

