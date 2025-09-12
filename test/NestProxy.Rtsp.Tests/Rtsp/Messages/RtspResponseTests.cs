using NestProxy.Rtsp.Messages;
using Xunit;

namespace NestProxy.Rtsp.Tests.Rtsp.Messages;

public class RtspResponseTests
{
    [Fact]
    public void Constructor_InitializesEmptyReasonPhrase()
    {
        var response = new RtspResponse();

        Assert.Equal(string.Empty, response.ReasonPhrase);
    }

    [Fact]
    public void ReturnCode_CanBeSetAndRetrieved()
    {
        var response = new RtspResponse();

        response.ReturnCode = 200;

        Assert.Equal(200, response.ReturnCode);
    }

    [Fact]
    public void ReasonPhrase_CanBeSetAndRetrieved()
    {
        var response = new RtspResponse();

        response.ReasonPhrase = "OK";

        Assert.Equal("OK", response.ReasonPhrase);
    }

    [Fact]
    public void OriginalRequest_CanBeSetAndRetrieved()
    {
        var response = new RtspResponse();
        var request = new RtspRequest { Method = "OPTIONS" };

        response.OriginalRequest = request;

        Assert.Same(request, response.OriginalRequest);
    }

    [Theory]
    [InlineData(200, true)]
    [InlineData(201, true)]
    [InlineData(250, true)]
    [InlineData(299, true)]
    [InlineData(199, false)]
    [InlineData(300, false)]
    [InlineData(400, false)]
    [InlineData(404, false)]
    [InlineData(500, false)]
    public void IsSuccessStatusCode_ReturnsCorrectValue(int statusCode, bool expectedSuccess)
    {
        var response = new RtspResponse { ReturnCode = statusCode };

        var isSuccess = response.IsSuccessStatusCode;

        Assert.Equal(expectedSuccess, isSuccess);
    }

    [Fact]
    public void Response_InheritsFromRtspMessage()
    {
        var response = new RtspResponse
        {
            CSeq = 10,
            Session = "XYZ789",
            ReturnCode = 200
        };

        Assert.Equal(10, response.CSeq);
        Assert.Equal("XYZ789", response.Session);
        Assert.IsAssignableFrom<RtspMessage>(response);
    }
}
