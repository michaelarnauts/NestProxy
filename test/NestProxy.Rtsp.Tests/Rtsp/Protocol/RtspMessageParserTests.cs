using System.Text;
using NestProxy.Rtsp.Messages;
using NestProxy.Rtsp.Protocol;
using Xunit;

namespace NestProxy.Rtsp.Tests.Rtsp.Protocol;

public class RtspMessageParserTests
{
    [Fact]
    public async Task ParseAsync_WithOptionsRequest_ParsesCorrectly()
    {
        var parser = new RtspMessageParser();
        var message = "OPTIONS rtsp://example.com:554/stream RTSP/1.0\r\n" +
                     "CSeq: 1\r\n" +
                     "\r\n";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(message));

        var result = await parser.ParseAsync(stream);

        Assert.NotNull(result);
        var request = Assert.IsType<RtspRequest>(result);
        Assert.Equal("OPTIONS", request.Method);
        Assert.Equal("rtsp://example.com:554/stream", request.RtspUri.ToString());
        Assert.Equal(1, request.CSeq);
    }

    [Fact]
    public async Task ParseAsync_WithSuccessResponse_ParsesCorrectly()
    {
        var parser = new RtspMessageParser();
        var message = "RTSP/1.0 200 OK\r\n" +
                     "CSeq: 5\r\n" +
                     "Session: ABC123\r\n" +
                     "\r\n";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(message));

        var result = await parser.ParseAsync(stream);

        Assert.NotNull(result);
        var response = Assert.IsType<RtspResponse>(result);
        Assert.Equal(200, response.ReturnCode);
        Assert.Equal("OK", response.ReasonPhrase);
        Assert.Equal(5, response.CSeq);
        Assert.Equal("ABC123", response.Session);
    }

    [Fact]
    public async Task ParseAsync_WithMessageBody_ParsesBodyCorrectly()
    {
        var parser = new RtspMessageParser();
        var body = "v=0\r\no=- 123 456 IN IP4 127.0.0.1\r\n";
        var message = "RTSP/1.0 200 OK\r\n" +
                     "CSeq: 10\r\n" +
                     $"Content-Length: {body.Length}\r\n" +
                     "Content-Type: application/sdp\r\n" +
                     "\r\n" +
                     body;
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(message));

        var result = await parser.ParseAsync(stream);

        Assert.NotNull(result);
        var response = Assert.IsType<RtspResponse>(result);
        Assert.Equal(body, response.GetBodyAsString());
        Assert.Equal("application/sdp", response.ContentType);
    }

    [Fact]
    public async Task ParseAsync_WithInterleavedFrame_ParsesFrameCorrectly()
    {
        var parser = new RtspMessageParser();
        var frameData = new byte[] { 0x80, 0x60, 0x12, 0x34, 0x56, 0x78 };
        var message = new byte[10];
        message[0] = (byte)'$';
        message[1] = 2;
        message[2] = 0;
        message[3] = 6;
        Array.Copy(frameData, 0, message, 4, frameData.Length);

        var stream = new MemoryStream(message);

        var result = await parser.ParseAsync(stream);

        Assert.NotNull(result);
        var frame = Assert.IsType<InterleavedFrame>(result);
        Assert.Equal(2, frame.Channel);
        Assert.Equal(6, frame.Data.Length);
        Assert.Equal(frameData, frame.Data.ToArray());
    }

    [Fact]
    public async Task ParseAsync_WithMultipleInterleavedFramesInSingleBuffer_PreservesRemainingData()
    {
        RtspMessageParser parser = new();
        byte[] firstPayload = [0x80, 0x60, 0x00, 0x01];
        byte[] secondPayload = [0x81, 0xC8, 0x00, 0x01, 0xAA];

        byte[] frame1 = BuildFrame(0, firstPayload);
        byte[] frame2 = BuildFrame(1, secondPayload);

        byte[] combined = new byte[frame1.Length + frame2.Length];
        Array.Copy(frame1, 0, combined, 0, frame1.Length);
        Array.Copy(frame2, 0, combined, frame1.Length, frame2.Length);

        using MemoryStream stream = new MemoryStream(combined);

        object? first = await parser.ParseAsync(stream);
        InterleavedFrame firstFrame = Assert.IsType<InterleavedFrame>(first);
        Assert.Equal((byte)0, firstFrame.Channel);
        Assert.Equal(firstPayload, firstFrame.Data.ToArray());

        object? second = await parser.ParseAsync(stream);
        InterleavedFrame secondFrame = Assert.IsType<InterleavedFrame>(second);
        Assert.Equal((byte)1, secondFrame.Channel);
        Assert.Equal(secondPayload, secondFrame.Data.ToArray());

        firstFrame.Dispose();
        secondFrame.Dispose();

        byte[] BuildFrame(byte channel, byte[] payload)
        {
            byte[] frame = new byte[4 + payload.Length];
            frame[0] = (byte)'$';
            frame[1] = channel;
            frame[2] = (byte)(payload.Length >> 8);
            frame[3] = (byte)(payload.Length & 0xFF);
            Array.Copy(payload, 0, frame, 4, payload.Length);
            return frame;
        }
    }

    [Fact]
    public async Task ParseAsync_WithMultipleHeadersOnSameName_LastValueWins()
    {
        var parser = new RtspMessageParser();
        var message = "RTSP/1.0 200 OK\r\n" +
                     "CSeq: 1\r\n" +
                     "Session: First\r\n" +
                     "Session: Second\r\n" +
                     "\r\n";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(message));

        var result = await parser.ParseAsync(stream);

        Assert.NotNull(result);
        var response = Assert.IsType<RtspResponse>(result);
        Assert.Equal("Second", response.Session);
    }

    [Fact]
    public async Task ParseAsync_WithDescribeRequest_ParsesAllHeaders()
    {
        var parser = new RtspMessageParser();
        var message = "DESCRIBE rtsp://example.com/stream RTSP/1.0\r\n" +
                     "CSeq: 2\r\n" +
                     "Accept: application/sdp\r\n" +
                     "User-Agent: TestClient/1.0\r\n" +
                     "\r\n";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(message));

        var result = await parser.ParseAsync(stream);

        Assert.NotNull(result);
        var request = Assert.IsType<RtspRequest>(result);
        Assert.Equal("DESCRIBE", request.Method);
        Assert.Equal(2, request.CSeq);
        Assert.Equal("application/sdp", request.Headers["Accept"]);
        Assert.Equal("TestClient/1.0", request.Headers["User-Agent"]);
    }

    [Fact]
    public async Task ParseAsync_HeaderNamesAreCaseInsensitive()
    {
        var parser = new RtspMessageParser();
        var message = "RTSP/1.0 200 OK\r\n" +
                     "cseq: 1\r\n" +
                     "CONTENT-LENGTH: 0\r\n" +
                     "\r\n";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(message));

        var result = await parser.ParseAsync(stream);

        Assert.NotNull(result);
        var response = Assert.IsType<RtspResponse>(result);
        Assert.Equal(1, response.CSeq);
        Assert.True(response.Headers.ContainsKey("cseq"));
        Assert.True(response.Headers.ContainsKey("CONTENT-LENGTH"));
    }

    [Fact]
    public async Task ParseAsync_WithSetupRequest_ParsesTransportHeader()
    {
        var parser = new RtspMessageParser();
        var message = "SETUP rtsp://example.com/stream/track1 RTSP/1.0\r\n" +
                     "CSeq: 3\r\n" +
                     "Transport: RTP/AVP/TCP;interleaved=0-1\r\n" +
                     "\r\n";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(message));

        var result = await parser.ParseAsync(stream);

        Assert.NotNull(result);
        var request = Assert.IsType<RtspRequest>(result);
        Assert.Equal("SETUP", request.Method);
        Assert.Contains("Transport", request.Headers.Keys);
        Assert.Equal("RTP/AVP/TCP;interleaved=0-1", request.Headers["Transport"]);
    }

    [Fact]
    public async Task ParseAsync_Reset_ClearsParserState()
    {
        var parser = new RtspMessageParser();
        var message1 = "OPTIONS rtsp://example.com/stream RTSP/1.0\r\nCSeq: 1\r\n\r\n";
        var stream1 = new MemoryStream(Encoding.UTF8.GetBytes(message1));

        await parser.ParseAsync(stream1);

        parser.Reset();

        Assert.Equal(RtspParserState.WaitingForMessage, parser.State);

        var message2 = "RTSP/1.0 200 OK\r\nCSeq: 2\r\n\r\n";
        var stream2 = new MemoryStream(Encoding.UTF8.GetBytes(message2));
        var result = await parser.ParseAsync(stream2);

        Assert.NotNull(result);
        Assert.IsType<RtspResponse>(result);
    }

    [Fact]
    public async Task ParseAsync_WithLargeBody_ParsesCorrectly()
    {
        var parser = new RtspMessageParser();
        var body = new string('X', 5000);
        var message = "RTSP/1.0 200 OK\r\n" +
                     "CSeq: 1\r\n" +
                     $"Content-Length: {body.Length}\r\n" +
                     "\r\n" +
                     body;
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(message));

        var result = await parser.ParseAsync(stream);

        Assert.NotNull(result);
        var response = Assert.IsType<RtspResponse>(result);
        Assert.Equal(body, response.GetBodyAsString());
    }

    [Fact]
    public async Task ParseAsync_RoundTrip_WithSerializer()
    {
        var serializer = new RtspMessageSerializer();
        var parser = new RtspMessageParser();

        var original = new RtspRequest
        {
            Method = "PLAY",
            RtspUri = new Uri("rtsp://example.com/stream"),
            CSeq = 7,
            Session = "XYZ789"
        };
        original.Headers["Range"] = "npt=0.000-";

        var serializeStream = new MemoryStream();
        await serializer.SerializeAsync(original, serializeStream);

        serializeStream.Position = 0;

        var result = await parser.ParseAsync(serializeStream);

        Assert.NotNull(result);
        var parsed = Assert.IsType<RtspRequest>(result);
        Assert.Equal(original.Method, parsed.Method);
        Assert.Equal(original.RtspUri.ToString(), parsed.RtspUri.ToString());
        Assert.Equal(original.CSeq, parsed.CSeq);
        Assert.Equal(original.Session, parsed.Session);
        Assert.Equal("npt=0.000-", parsed.Headers["Range"]);
    }
}
