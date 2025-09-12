using System.Text;

using NestProxy.Rtsp.Messages;
using NestProxy.Rtsp.Protocol;

using Xunit;

namespace NestProxy.Tests.Rtsp;

/// <summary>
/// Tests for RTSP protocol message parsing and serialization per RFC 2326.
/// </summary>
public class RtspProtocolTests
{
    [Fact]
    public async Task ParseRequest_ValidOptionsRequest_ParsesCorrectly()
    {
        // Arrange
        var requestText = "OPTIONS rtsp://example.com/media.mp4 RTSP/1.0\r\n" +
                         "CSeq: 1\r\n" +
                         "User-Agent: TestClient/1.0\r\n" +
                         "\r\n";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(requestText));
        var parser = new RtspMessageParser();

        // Act
        var message = await parser.ParseAsync(stream);

        // Assert
        Assert.NotNull(message);
        Assert.IsType<RtspRequest>(message);
        var request = (RtspRequest)message;
        Assert.Equal(RtspMethod.OPTIONS, request.Method);
        Assert.Equal("rtsp://example.com/media.mp4", request.RtspUri.ToString());
        Assert.Equal("RTSP/1.0", request.Version);
        Assert.Equal("1", request.Headers["CSeq"]);
        Assert.Equal("TestClient/1.0", request.Headers["User-Agent"]);
    }

    [Fact]
    public async Task ParseRequest_ValidDescribeRequest_ParsesCorrectly()
    {
        // Arrange
        var requestText = "DESCRIBE rtsp://example.com/media.mp4 RTSP/1.0\r\n" +
                         "CSeq: 2\r\n" +
                         "Accept: application/sdp\r\n" +
                         "\r\n";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(requestText));
        var parser = new RtspMessageParser();

        // Act
        var message = await parser.ParseAsync(stream);

        // Assert
        Assert.NotNull(message);
        Assert.IsType<RtspRequest>(message);
        var request = (RtspRequest)message;
        Assert.Equal(RtspMethod.DESCRIBE, request.Method);
        Assert.Equal("2", request.Headers["CSeq"]);
        Assert.Equal("application/sdp", request.Headers["Accept"]);
    }

    [Fact]
    public async Task ParseRequest_ValidSetupRequest_ParsesCorrectly()
    {
        // Arrange
        var requestText = "SETUP rtsp://example.com/media.mp4/track1 RTSP/1.0\r\n" +
                         "CSeq: 3\r\n" +
                         "Transport: RTP/AVP/TCP;unicast;interleaved=0-1\r\n" +
                         "\r\n";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(requestText));
        var parser = new RtspMessageParser();

        // Act
        var message = await parser.ParseAsync(stream);

        // Assert
        Assert.NotNull(message);
        Assert.IsType<RtspRequest>(message);
        var request = (RtspRequest)message;
        Assert.Equal(RtspMethod.SETUP, request.Method);
        Assert.Equal("3", request.Headers["CSeq"]);
        Assert.Contains("RTP/AVP/TCP", request.Headers["Transport"]);
        Assert.Contains("interleaved=0-1", request.Headers["Transport"]);
    }

    [Fact]
    public async Task ParseRequest_ValidPlayRequest_ParsesCorrectly()
    {
        // Arrange
        var requestText = "PLAY rtsp://example.com/media.mp4 RTSP/1.0\r\n" +
                         "CSeq: 5\r\n" +
                         "Session: 12345678\r\n" +
                         "Range: npt=0.000-\r\n" +
                         "\r\n";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(requestText));
        var parser = new RtspMessageParser();

        // Act
        var message = await parser.ParseAsync(stream);

        // Assert
        Assert.NotNull(message);
        Assert.IsType<RtspRequest>(message);
        var request = (RtspRequest)message;
        Assert.Equal(RtspMethod.PLAY, request.Method);
        Assert.Equal("5", request.Headers["CSeq"]);
        Assert.Equal("12345678", request.Headers["Session"]);
        Assert.Equal("npt=0.000-", request.Headers["Range"]);
    }

    [Fact]
    public async Task ParseRequest_WithBody_ParsesCorrectly()
    {
        // Arrange
        var body = "param1: value1\r\nparam2: value2\r\n";
        var requestText = "SET_PARAMETER rtsp://example.com/media.mp4 RTSP/1.0\r\n" +
                         "CSeq: 10\r\n" +
                         "Session: 12345678\r\n" +
                         $"Content-Length: {body.Length}\r\n" +
                         "Content-Type: text/parameters\r\n" +
                         "\r\n" +
                         body;
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(requestText));
        var parser = new RtspMessageParser();

        // Act
        var message = await parser.ParseAsync(stream);

        // Assert
        Assert.NotNull(message);
        Assert.IsType<RtspRequest>(message);
        var request = (RtspRequest)message;
        Assert.Equal(RtspMethod.SET_PARAMETER, request.Method);
        Assert.Equal(body.Length, request.Data.Length);
        Assert.Equal(body, Encoding.UTF8.GetString(request.Data.ToArray()));
    }

    [Fact]
    public async Task ParseResponse_Valid200Response_ParsesCorrectly()
    {
        // Arrange
        var responseText = "RTSP/1.0 200 OK\r\n" +
                          "CSeq: 1\r\n" +
                          "Public: OPTIONS, DESCRIBE, SETUP, PLAY, PAUSE, TEARDOWN\r\n" +
                          "\r\n";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(responseText));
        var parser = new RtspMessageParser();

        // Act
        var message = await parser.ParseAsync(stream);

        // Assert
        Assert.NotNull(message);
        Assert.IsType<RtspResponse>(message);
        var response = (RtspResponse)message;
        Assert.Equal(RtspStatusCode.OK, response.StatusCode);
        Assert.Equal("OK", response.ReasonPhrase);
        Assert.Equal("RTSP/1.0", response.Version);
        Assert.Equal("1", response.Headers["CSeq"]);
    }

    [Fact]
    public async Task ParseResponse_WithSdpBody_ParsesCorrectly()
    {
        // Arrange
        var sdp = "v=0\r\no=- 123 456 IN IP4 127.0.0.1\r\ns=Test\r\n";
        var responseText = "RTSP/1.0 200 OK\r\n" +
                          "CSeq: 2\r\n" +
                          "Content-Type: application/sdp\r\n" +
                          $"Content-Length: {sdp.Length}\r\n" +
                          "\r\n" +
                          sdp;
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(responseText));
        var parser = new RtspMessageParser();

        // Act
        var message = await parser.ParseAsync(stream);

        // Assert
        Assert.NotNull(message);
        Assert.IsType<RtspResponse>(message);
        var response = (RtspResponse)message;
        Assert.Equal(RtspStatusCode.OK, response.StatusCode);
        Assert.Equal("application/sdp", response.Headers["Content-Type"]);
        Assert.Equal(sdp.Length, response.Data.Length);
        Assert.Equal(sdp, Encoding.UTF8.GetString(response.Data.ToArray()));
    }

    [Fact]
    public async Task ParseResponse_ErrorStatus_ParsesCorrectly()
    {
        // Arrange
        var responseText = "RTSP/1.0 404 Not Found\r\n" +
                          "CSeq: 5\r\n" +
                          "\r\n";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(responseText));
        var parser = new RtspMessageParser();

        // Act
        var message = await parser.ParseAsync(stream);

        // Assert
        Assert.NotNull(message);
        Assert.IsType<RtspResponse>(message);
        var response = (RtspResponse)message;
        Assert.Equal(RtspStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Not Found", response.ReasonPhrase);
    }

    [Fact]
    public async Task SerializeRequest_ValidOptionsRequest_SerializesCorrectly()
    {
        // Arrange
        var request = new RtspRequest(RtspMethod.OPTIONS, new Uri("rtsp://example.com/test"), "RTSP/1.0");
        request.Headers["CSeq"] = "1";
        var stream = new MemoryStream();
        var serializer = new RtspMessageSerializer();

        // Act
        await serializer.SerializeAsync(request, stream);

        // Assert
        stream.Position = 0;
        var result = Encoding.UTF8.GetString(stream.ToArray());
        Assert.Contains("OPTIONS rtsp://example.com/test RTSP/1.0", result);
        Assert.Contains("CSeq: 1", result);
        Assert.EndsWith("\r\n\r\n", result);
    }

    [Fact]
    public async Task SerializeRequest_WithBody_SerializesCorrectly()
    {
        // Arrange
        var body = "volume: 80\r\n";
        var request = new RtspRequest(RtspMethod.SET_PARAMETER, new Uri("rtsp://example.com/test"), "RTSP/1.0");
        request.Headers["CSeq"] = "10";
        request.Headers["Session"] = "12345";
        request.Data = Encoding.UTF8.GetBytes(body);
        var stream = new MemoryStream();
        var serializer = new RtspMessageSerializer();

        // Act
        await serializer.SerializeAsync(request, stream);

        // Assert
        stream.Position = 0;
        var result = Encoding.UTF8.GetString(stream.ToArray());
        Assert.Contains("SET_PARAMETER rtsp://example.com/test RTSP/1.0", result);
        Assert.Contains("CSeq: 10", result);
        Assert.Contains("Session: 12345", result);
        Assert.EndsWith(body, result);
    }

    [Fact]
    public async Task SerializeResponse_Valid200Response_SerializesCorrectly()
    {
        // Arrange
        var response = new RtspResponse(RtspStatusCode.OK, "OK", "RTSP/1.0");
        response.Headers["CSeq"] = "1";
        response.Headers["Public"] = "OPTIONS, DESCRIBE";
        var stream = new MemoryStream();
        var serializer = new RtspMessageSerializer();

        // Act
        await serializer.SerializeAsync(response, stream);

        // Assert
        stream.Position = 0;
        var result = Encoding.UTF8.GetString(stream.ToArray());
        Assert.Contains("RTSP/1.0 200 OK", result);
        Assert.Contains("CSeq: 1", result);
        Assert.Contains("Public: OPTIONS, DESCRIBE", result);
        Assert.EndsWith("\r\n\r\n", result);
    }

    [Fact]
    public async Task SerializeResponse_WithBody_SerializesCorrectly()
    {
        // Arrange
        var sdp = "v=0\r\ns=Test\r\n";
        var response = new RtspResponse(RtspStatusCode.OK, "OK", "RTSP/1.0");
        response.Headers["CSeq"] = "2";
        response.Headers["Content-Type"] = "application/sdp";
        response.Data = Encoding.UTF8.GetBytes(sdp);
        var stream = new MemoryStream();
        var serializer = new RtspMessageSerializer();

        // Act
        await serializer.SerializeAsync(response, stream);

        // Assert
        stream.Position = 0;
        var result = Encoding.UTF8.GetString(stream.ToArray());
        Assert.Contains("RTSP/1.0 200 OK", result);
        Assert.Contains("Content-Type: application/sdp", result);
        Assert.EndsWith(sdp, result);
    }

    [Fact]
    public async Task ParseInterleaved_ValidFrame_ParsesCorrectly()
    {
        // Arrange
        var payload = new byte[] { 0x80, 0x60, 0x01, 0x02, 0x03, 0x04 };
        var frame = new byte[4 + payload.Length];
        frame[0] = (byte)'$';      // Magic byte
        frame[1] = 0;              // Channel
        frame[2] = 0x00;           // Length MSB
        frame[3] = 0x06;           // Length LSB
        Array.Copy(payload, 0, frame, 4, payload.Length);

        var stream = new MemoryStream(frame);
        var parser = new RtspMessageParser();

        // Act
        var message = await parser.ParseAsync(stream);

        // Assert
        Assert.NotNull(message);
        Assert.IsType<InterleavedFrame>(message);
        var interleavedFrame = (InterleavedFrame)message;
        Assert.Equal(0, interleavedFrame.Channel);
        Assert.Equal(payload.Length, interleavedFrame.Data.Length);
        Assert.Equal(payload, interleavedFrame.Data.ToArray());
    }

    [Fact]
    public async Task SerializeInterleaved_ValidFrame_SerializesCorrectly()
    {
        // Arrange
        var payload = new byte[] { 0x80, 0x60, 0x01, 0x02 };
        var frame = new InterleavedFrame((byte)2, payload);
        var stream = new MemoryStream();
        var serializer = new RtspMessageSerializer();

        // Act
        await serializer.SerializeAsync(frame, stream);

        // Assert
        var result = stream.ToArray();
        Assert.Equal(4 + payload.Length, result.Length);
        Assert.Equal((byte)'$', result[0]);
        Assert.Equal(2, result[1]);
        Assert.Equal(0x00, result[2]); // Length MSB
        Assert.Equal(0x04, result[3]); // Length LSB
        Assert.Equal(payload, result.Skip(4).ToArray());
    }

    [Fact]
    public async Task ParseMultipleMessages_RequestAndInterleaved_ParsesCorrectly()
    {
        // Arrange
        var requestText = "OPTIONS rtsp://example.com/test RTSP/1.0\r\nCSeq: 1\r\n\r\n";
        var interleavedData = new byte[] { (byte)'$', 0, 0, 4, 0x01, 0x02, 0x03, 0x04 };

        var combined = new byte[Encoding.UTF8.GetByteCount(requestText) + interleavedData.Length];
        Encoding.UTF8.GetBytes(requestText, 0, requestText.Length, combined, 0);
        Array.Copy(interleavedData, 0, combined, Encoding.UTF8.GetByteCount(requestText), interleavedData.Length);

        var stream = new MemoryStream(combined);
        var parser = new RtspMessageParser();

        // Act
        var message1 = await parser.ParseAsync(stream);
        var message2 = await parser.ParseAsync(stream);

        // Assert
        Assert.NotNull(message1);
        Assert.IsType<RtspRequest>(message1);
        Assert.NotNull(message2);
        Assert.IsType<InterleavedFrame>(message2);
    }

    [Fact]
    public async Task ParseRequest_CaseInsensitiveHeaders_ParsesCorrectly()
    {
        // Arrange
        var requestText = "OPTIONS rtsp://example.com/test RTSP/1.0\r\n" +
                         "cseq: 1\r\n" +
                         "USER-AGENT: TestClient\r\n" +
                         "\r\n";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(requestText));
        var parser = new RtspMessageParser();

        // Act
        var message = await parser.ParseAsync(stream);

        // Assert
        Assert.NotNull(message);
        Assert.IsType<RtspRequest>(message);
        var request = (RtspRequest)message;
        // Headers should be case-insensitive
        Assert.True(request.Headers.ContainsKey("cseq") || request.Headers.ContainsKey("CSeq"));
    }

    [Fact]
    public async Task RoundTrip_RequestSerializeAndParse_PreservesData()
    {
        // Arrange
        var originalRequest = new RtspRequest(RtspMethod.PLAY, new Uri("rtsp://example.com/test"), "RTSP/1.0");
        originalRequest.Headers["CSeq"] = "5";
        originalRequest.Headers["Session"] = "abc123";
        originalRequest.Headers["Range"] = "npt=10.0-20.0";

        var stream = new MemoryStream();
        var serializer = new RtspMessageSerializer();
        var parser = new RtspMessageParser();

        // Act
        await serializer.SerializeAsync(originalRequest, stream);
        stream.Position = 0;
        var message = await parser.ParseAsync(stream);

        // Assert
        Assert.NotNull(message);
        Assert.IsType<RtspRequest>(message);
        var parsedRequest = (RtspRequest)message;
        Assert.Equal(originalRequest.Method, parsedRequest.Method);
        Assert.Equal(originalRequest.RtspUri.ToString(), parsedRequest.RtspUri.ToString());
        Assert.Equal(originalRequest.Version, parsedRequest.Version);
        Assert.Equal(originalRequest.Headers["CSeq"], parsedRequest.Headers["CSeq"]);
        Assert.Equal(originalRequest.Headers["Session"], parsedRequest.Headers["Session"]);
        Assert.Equal(originalRequest.Headers["Range"], parsedRequest.Headers["Range"]);
    }

    [Fact]
    public async Task RoundTrip_ResponseSerializeAndParse_PreservesData()
    {
        // Arrange
        var originalResponse = new RtspResponse(RtspStatusCode.OK, "OK", "RTSP/1.0");
        originalResponse.Headers["CSeq"] = "3";
        originalResponse.Headers["Session"] = "xyz789";
        originalResponse.Headers["Transport"] = "RTP/AVP/TCP;interleaved=0-1";

        var stream = new MemoryStream();
        var serializer = new RtspMessageSerializer();
        var parser = new RtspMessageParser();

        // Act
        await serializer.SerializeAsync(originalResponse, stream);
        stream.Position = 0;
        var message = await parser.ParseAsync(stream);

        // Assert
        Assert.NotNull(message);
        Assert.IsType<RtspResponse>(message);
        var parsedResponse = (RtspResponse)message;
        Assert.Equal(originalResponse.StatusCode, parsedResponse.StatusCode);
        Assert.Equal(originalResponse.ReasonPhrase, parsedResponse.ReasonPhrase);
        Assert.Equal(originalResponse.Version, parsedResponse.Version);
        Assert.Equal(originalResponse.Headers["CSeq"], parsedResponse.Headers["CSeq"]);
        Assert.Equal(originalResponse.Headers["Session"], parsedResponse.Headers["Session"]);
        Assert.Equal(originalResponse.Headers["Transport"], parsedResponse.Headers["Transport"]);
    }

    [Fact]
    public void CreateResponse_FromRequest_CopiesCSeq()
    {
        // Arrange
        var request = new RtspRequest(RtspMethod.OPTIONS, new Uri("rtsp://example.com/test"), "RTSP/1.0");
        request.Headers["CSeq"] = "42";

        // Act
        var response = request.CreateResponse((int)RtspStatusCode.OK);

        // Assert
        Assert.Equal(RtspStatusCode.OK, response.StatusCode);
        Assert.Equal("42", response.Headers["CSeq"]);
        Assert.Equal("RTSP/1.0", response.Version);
    }

    [Fact]
    public void CreateResponse_WithCustomStatusAndReason_SetsCorrectly()
    {
        // Arrange
        var request = new RtspRequest(RtspMethod.DESCRIBE, new Uri("rtsp://example.com/test"), "RTSP/1.0");
        request.Headers["CSeq"] = "2";

        // Act
        var response = request.CreateResponse((int)RtspStatusCode.NotFound, "Stream Not Found");

        // Assert
        Assert.Equal(RtspStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Stream Not Found", response.ReasonPhrase);
        Assert.Equal("2", response.Headers["CSeq"]);
    }
}

