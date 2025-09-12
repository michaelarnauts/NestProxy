using System.Text;
using NestProxy.Rtsp.Messages;
using Xunit;

namespace NestProxy.Rtsp.Tests.Rtsp.Messages;

public class RtspMessageTests
{
    private class TestMessage : RtspMessage { }

    [Fact]
    public void Constructor_InitializesDefaultVersion()
    {
        var message = new TestMessage();

        Assert.Equal("RTSP/1.0", message.Version);
    }

    [Fact]
    public void Headers_IsCaseInsensitive()
    {
        var message = new TestMessage();

        message.Headers["Content-Type"] = "application/sdp";

        Assert.Equal("application/sdp", message.Headers["content-type"]);
        Assert.Equal("application/sdp", message.Headers["CONTENT-TYPE"]);
        Assert.Equal("application/sdp", message.Headers["Content-Type"]);
    }

    [Fact]
    public void CSeq_GetSet_WorksCorrectly()
    {
        var message = new TestMessage();

        message.CSeq = 42;

        Assert.Equal(42, message.CSeq);
        Assert.Equal("42", message.Headers["CSeq"]);
    }

    [Fact]
    public void CSeq_WithInvalidHeader_ReturnsZero()
    {
        var message = new TestMessage();
        message.Headers["CSeq"] = "invalid";

        var cseq = message.CSeq;

        Assert.Equal(0, cseq);
    }

    [Fact]
    public void Session_GetSet_WorksCorrectly()
    {
        var message = new TestMessage();

        message.Session = "ABC123";

        Assert.Equal("ABC123", message.Session);
        Assert.Equal("ABC123", message.Headers["Session"]);
    }

    [Fact]
    public void Session_SetToNull_RemovesHeader()
    {
        var message = new TestMessage();
        message.Session = "ABC123";

        message.Session = null;

        Assert.Null(message.Session);
        Assert.False(message.Headers.ContainsKey("Session"));
    }

    [Fact]
    public void ContentType_GetSet_WorksCorrectly()
    {
        var message = new TestMessage();

        message.ContentType = "application/sdp";

        Assert.Equal("application/sdp", message.ContentType);
        Assert.Equal("application/sdp", message.Headers["Content-Type"]);
    }

    [Fact]
    public void ContentType_SetToNull_RemovesHeader()
    {
        var message = new TestMessage();
        message.ContentType = "application/sdp";

        message.ContentType = null;

        Assert.Null(message.ContentType);
        Assert.False(message.Headers.ContainsKey("Content-Type"));
    }

    [Fact]
    public void ContentLength_WithExplicitHeader_ReturnsHeaderValue()
    {
        var message = new TestMessage();
        message.Headers["Content-Length"] = "100";

        var length = message.ContentLength;

        Assert.Equal(100, length);
    }

    [Fact]
    public void ContentLength_WithoutHeader_ReturnsDataLength()
    {
        var message = new TestMessage();
        message.Data = new byte[] { 1, 2, 3, 4, 5 };

        var length = message.ContentLength;

        Assert.Equal(5, length);
    }

    [Fact]
    public void ContentLength_Set_UpdatesHeader()
    {
        var message = new TestMessage();

        message.ContentLength = 42;

        Assert.Equal("42", message.Headers["Content-Length"]);
    }

    [Fact]
    public void GetBodyAsString_WithEmptyData_ReturnsNull()
    {
        var message = new TestMessage();

        var body = message.GetBodyAsString();

        Assert.Null(body);
    }

    [Fact]
    public void GetBodyAsString_WithData_ReturnsUtf8String()
    {
        var message = new TestMessage();
        var expectedText = "Hello, RTSP!";
        message.Data = Encoding.UTF8.GetBytes(expectedText);

        var body = message.GetBodyAsString();

        Assert.Equal(expectedText, body);
    }

    [Fact]
    public void SetBodyFromString_WithValidString_SetsDataAndContentLength()
    {
        var message = new TestMessage();
        var text = "Test Body";

        message.SetBodyFromString(text);

        Assert.Equal(text, Encoding.UTF8.GetString(message.Data.Span));
        Assert.Equal(text.Length, message.ContentLength);
    }

    [Fact]
    public void SetBodyFromString_WithEmptyString_ClearsData()
    {
        var message = new TestMessage();
        message.Data = new byte[] { 1, 2, 3 };

        message.SetBodyFromString("");

        Assert.Equal(0, message.Data.Length);
    }

    [Fact]
    public void SetBodyFromString_WithNull_ClearsData()
    {
        var message = new TestMessage();
        message.Data = new byte[] { 1, 2, 3 };

        message.SetBodyFromString(null!);

        Assert.Equal(0, message.Data.Length);
    }

    [Fact]
    public void Data_DefaultValue_IsEmpty()
    {
        var message = new TestMessage();

        Assert.Equal(0, message.Data.Length);
    }
}
