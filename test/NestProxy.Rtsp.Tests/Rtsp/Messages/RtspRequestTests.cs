using NestProxy.Rtsp.Messages;
using Xunit;

namespace NestProxy.Rtsp.Tests.Rtsp.Messages;

public class RtspRequestTests
{
    [Fact]
    public void Constructor_InitializesEmptyMethod()
    {
        var request = new RtspRequest();

        Assert.Equal(string.Empty, request.Method);
    }

    [Fact]
    public void Method_CanBeSetAndRetrieved()
    {
        var request = new RtspRequest();

        request.Method = "OPTIONS";

        Assert.Equal("OPTIONS", request.Method);
    }

    [Fact]
    public void RtspUri_CanBeSetAndRetrieved()
    {
        var request = new RtspRequest();
        var uri = new Uri("rtsp://example.com:554/stream");

        request.RtspUri = uri;

        Assert.Equal(uri, request.RtspUri);
    }

    [Fact]
    public void CreateResponse_WithDefaults_ReturnsSuccessResponse()
    {
        var request = new RtspRequest
        {
            CSeq = 5,
            Version = "RTSP/1.0"
        };

        var response = request.CreateResponse();

        Assert.Equal(200, response.ReturnCode);
        Assert.Equal("OK", response.ReasonPhrase);
        Assert.Equal(5, response.CSeq);
        Assert.Equal("RTSP/1.0", response.Version);
        Assert.Same(request, response.OriginalRequest);
    }

    [Fact]
    public void CreateResponse_WithCustomStatusAndReason_ReturnsConfiguredResponse()
    {
        var request = new RtspRequest
        {
            CSeq = 10
        };

        var response = request.CreateResponse(404, "Not Found");

        Assert.Equal(404, response.ReturnCode);
        Assert.Equal("Not Found", response.ReasonPhrase);
        Assert.Equal(10, response.CSeq);
    }

    [Fact]
    public void Clone_CreatesDeepCopy()
    {
        var original = new RtspRequest
        {
            Method = "SETUP",
            RtspUri = new Uri("rtsp://example.com:554/track1"),
            Version = "RTSP/2.0",
            CSeq = 3,
            Session = "ABC123"
        };
        original.Headers["Custom-Header"] = "CustomValue";
        original.Data = new byte[] { 1, 2, 3 };

        var clone = original.Clone();

        Assert.NotSame(original, clone);
        Assert.Equal(original.Method, clone.Method);
        Assert.Equal(original.RtspUri, clone.RtspUri);
        Assert.Equal(original.Version, clone.Version);
        Assert.Equal(original.CSeq, clone.CSeq);
        Assert.Equal(original.Session, clone.Session);
        Assert.Equal("CustomValue", clone.Headers["Custom-Header"]);
        Assert.True(original.Data.Span.SequenceEqual(clone.Data.Span));
    }

    [Fact]
    public void Clone_ModifyingClone_DoesNotAffectOriginal()
    {
        var original = new RtspRequest
        {
            Method = "PLAY",
            CSeq = 5
        };
        var clone = original.Clone();

        clone.Method = "PAUSE";
        clone.CSeq = 10;

        Assert.Equal("PLAY", original.Method);
        Assert.Equal(5, original.CSeq);
        Assert.Equal("PAUSE", clone.Method);
        Assert.Equal(10, clone.CSeq);
    }
}
