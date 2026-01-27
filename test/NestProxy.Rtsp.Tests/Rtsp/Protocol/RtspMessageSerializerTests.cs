using System.Text;

using NestProxy.Rtsp.Messages;
using NestProxy.Rtsp.Protocol;

using Xunit;

namespace NestProxy.Rtsp.Tests.Rtsp.Protocol;

public class RtspMessageSerializerTests
{
    private readonly RtspMessageSerializer _serializer = new();

    [Fact]
    public async Task SerializeRequest_WithBasicRequest_FormatsCorrectly()
    {
        var request = new RtspRequestOptions
        {
            RtspUri = new Uri("rtsp://example.com:554/stream"),
            CSeq = 1
        };

        var stream = new MemoryStream();

        await _serializer.SerializeAsync(request, stream, TestContext.Current.CancellationToken);

        var result = Encoding.UTF8.GetString(stream.ToArray());
        Assert.Contains("OPTIONS rtsp://example.com:554/stream RTSP/1.0", result);
        Assert.Contains("CSeq: 1", result);
        Assert.Contains("\r\n\r\n", result);
    }

    [Fact]
    public async Task SerializeRequest_WithHeaders_IncludesAllHeaders()
    {
        var request = new RtspRequestDescribe
        {
            RtspUri = new Uri("rtsp://example.com/stream"),
            CSeq = 2,
            Session = "ABC123"
        };
        request.Headers["User-Agent"] = "TestClient/1.0";

        var stream = new MemoryStream();

        await _serializer.SerializeAsync(request, stream, TestContext.Current.CancellationToken);

        var result = Encoding.UTF8.GetString(stream.ToArray());
        Assert.Contains("DESCRIBE", result);
        Assert.Contains("CSeq: 2", result);
        Assert.Contains("Session: ABC123", result);
        Assert.Contains("User-Agent: TestClient/1.0", result);
        Assert.Contains("Accept: application/sdp", result);
    }

    [Fact]
    public async Task SerializeRequest_WithBody_IncludesContentLength()
    {
        var request = new RtspRequest
        {
            Method = "ANNOUNCE",
            RtspUri = new Uri("rtsp://example.com/stream"),
            CSeq = 3
        };
        request.SetBodyFromString("v=0\r\no=- 123 456 IN IP4 127.0.0.1\r\n");

        var stream = new MemoryStream();

        await _serializer.SerializeAsync(request, stream, TestContext.Current.CancellationToken);

        var result = Encoding.UTF8.GetString(stream.ToArray());
        Assert.Contains("Content-Length:", result);
        Assert.Contains("v=0", result);
    }

    [Fact]
    public async Task SerializeResponse_WithSuccessCode_FormatsCorrectly()
    {
        var response = new RtspResponse
        {
            Version = "RTSP/1.0",
            ReturnCode = 200,
            ReasonPhrase = "OK",
            CSeq = 5
        };

        var stream = new MemoryStream();

        await _serializer.SerializeAsync(response, stream, TestContext.Current.CancellationToken);

        var result = Encoding.UTF8.GetString(stream.ToArray());
        Assert.Contains("RTSP/1.0 200 OK", result);
        Assert.Contains("CSeq: 5", result);
        Assert.Contains("\r\n\r\n", result);
    }

    [Fact]
    public async Task SerializeResponse_WithBody_IncludesContentLength()
    {
        var response = new RtspResponse
        {
            ReturnCode = 200,
            ReasonPhrase = "OK",
            CSeq = 10
        };
        response.SetBodyFromString("Test SDP Content");

        var stream = new MemoryStream();

        await _serializer.SerializeAsync(response, stream, TestContext.Current.CancellationToken);

        var result = Encoding.UTF8.GetString(stream.ToArray());
        Assert.Contains("Content-Length: 16", result);
        Assert.Contains("Test SDP Content", result);
    }

    [Fact]
    public async Task SerializeInterleavedFrame_FormatsCorrectly()
    {
        var frameData = new byte[] { 0x80, 0x60, 0x12, 0x34 };
        var frame = new InterleavedFrame(0, frameData);

        var stream = new MemoryStream();

        await _serializer.SerializeAsync(frame, stream, TestContext.Current.CancellationToken);

        var result = stream.ToArray();
        Assert.Equal((byte)'$', result[0]);
        Assert.Equal(0, result[1]);
        Assert.Equal(0, result[2]);
        Assert.Equal(4, result[3]);
        Assert.Equal(0x80, result[4]);
        Assert.Equal(0x60, result[5]);
    }

    [Fact]
    public async Task SerializeInterleavedFrame_WithLargeData_EncodesLengthCorrectly()
    {
        var frameData = new byte[1500];
        var frame = new InterleavedFrame(2, frameData);

        var stream = new MemoryStream();

        await _serializer.SerializeAsync(frame, stream, TestContext.Current.CancellationToken);

        var result = stream.ToArray();
        Assert.Equal((byte)'$', result[0]);
        Assert.Equal(2, result[1]);

        var length = (result[2] << 8) | result[3];
        Assert.Equal(1500, length);
    }

    [Fact]
    public async Task SerializeRequest_AutoAddsCSeqIfMissing()
    {
        var request = new RtspRequest
        {
            Method = "OPTIONS",
            RtspUri = new Uri("rtsp://example.com/stream")
        };
        request.Headers.Remove("CSeq");
        request.CSeq = 42;

        var stream = new MemoryStream();

        await _serializer.SerializeAsync(request, stream, TestContext.Current.CancellationToken);

        var result = Encoding.UTF8.GetString(stream.ToArray());
        Assert.Contains("CSeq: 42", result);
    }
}
