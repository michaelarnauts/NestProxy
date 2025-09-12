using FluentAssertions;
using Moq;
using NestProxy.Rtsp.Client;
using NestProxy.Rtsp.Messages;
using NestProxy.Rtsp.Transport;
using Xunit;

namespace NestProxy.Rtsp.Tests;

public class RtspClientTests
{
    private readonly Mock<IRtspTransport> _mockTransport;
    private readonly RtspClient _client;

    public RtspClientTests()
    {
        _mockTransport = new Mock<IRtspTransport>();
        _client = new RtspClient(_mockTransport.Object);
    }

    [Fact]
    public async Task OptionsAsync_SendsCorrectRequest()
    {
        // Arrange
        var uri = new Uri("rtsp://example.com/stream");
        var response = new RtspResponse(RtspStatusCode.OK, "OK", RtspVersion.Version1_0);
        response.Headers["Public"] = "OPTIONS, DESCRIBE, SETUP, PLAY, TEARDOWN";

        _mockTransport.Setup(t => t.SendRequestAsync(It.IsAny<RtspRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        var result = await _client.OptionsAsync(uri);

        // Assert
        result.StatusCode.Should().Be(RtspStatusCode.OK);
        _mockTransport.Verify(t => t.SendRequestAsync(
            It.Is<RtspRequest>(r => r.Method == RtspMethod.OPTIONS && r.Uri == uri),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DescribeAsync_SendsCorrectRequest()
    {
        // Arrange
        var uri = new Uri("rtsp://example.com/stream");
        var sdpContent = "v=0\r\no=- 0 0 IN IP4 127.0.0.1\r\ns=Test\r\n";
        var response = new RtspResponse(RtspStatusCode.OK, "OK", RtspVersion.Version1_0);
        response.Headers["Content-Type"] = "application/sdp";
        response.Headers["Content-Length"] = sdpContent.Length.ToString();
        response.Body = sdpContent;

        _mockTransport.Setup(t => t.SendRequestAsync(It.IsAny<RtspRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        var result = await _client.DescribeAsync(uri);

        // Assert
        result.StatusCode.Should().Be(RtspStatusCode.OK);
        result.Body.Should().Be(sdpContent);
        _mockTransport.Verify(t => t.SendRequestAsync(
            It.Is<RtspRequest>(r => r.Method == RtspMethod.DESCRIBE && r.Uri == uri),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetupAsync_SendsCorrectRequest()
    {
        // Arrange
        var uri = new Uri("rtsp://example.com/stream/track1");
        var transport = "RTP/AVP/TCP;interleaved=0-1";
        var response = new RtspResponse(RtspStatusCode.OK, "OK", RtspVersion.Version1_0);
        response.Headers["Session"] = "12345678";
        response.Headers["Transport"] = transport;

        _mockTransport.Setup(t => t.SendRequestAsync(It.IsAny<RtspRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        var result = await _client.SetupAsync(uri, transport);

        // Assert
        result.StatusCode.Should().Be(RtspStatusCode.OK);
        result.Headers["Session"].Should().Be("12345678");
        _mockTransport.Verify(t => t.SendRequestAsync(
            It.Is<RtspRequest>(r => r.Method == RtspMethod.SETUP && 
                                     r.Uri == uri && 
                                     r.Headers["Transport"] == transport),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PlayAsync_SendsCorrectRequest()
    {
        // Arrange
        var uri = new Uri("rtsp://example.com/stream");
        var sessionId = "12345678";
        var response = new RtspResponse(RtspStatusCode.OK, "OK", RtspVersion.Version1_0);

        _mockTransport.Setup(t => t.SendRequestAsync(It.IsAny<RtspRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        var result = await _client.PlayAsync(uri, sessionId);

        // Assert
        result.StatusCode.Should().Be(RtspStatusCode.OK);
        _mockTransport.Verify(t => t.SendRequestAsync(
            It.Is<RtspRequest>(r => r.Method == RtspMethod.PLAY && 
                                     r.Uri == uri && 
                                     r.Headers["Session"] == sessionId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TeardownAsync_SendsCorrectRequest()
    {
        // Arrange
        var uri = new Uri("rtsp://example.com/stream");
        var sessionId = "12345678";
        var response = new RtspResponse(RtspStatusCode.OK, "OK", RtspVersion.Version1_0);

        _mockTransport.Setup(t => t.SendRequestAsync(It.IsAny<RtspRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        var result = await _client.TeardownAsync(uri, sessionId);

        // Assert
        result.StatusCode.Should().Be(RtspStatusCode.OK);
        _mockTransport.Verify(t => t.SendRequestAsync(
            It.Is<RtspRequest>(r => r.Method == RtspMethod.TEARDOWN && 
                                     r.Uri == uri && 
                                     r.Headers["Session"] == sessionId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Dispose_DisposesTransport()
    {
        // Act
        _client.Dispose();

        // Assert
        _mockTransport.Verify(t => t.Dispose(), Times.Once);
    }
}
