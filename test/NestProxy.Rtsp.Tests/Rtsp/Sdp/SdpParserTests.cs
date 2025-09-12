using NestProxy.Rtsp.Sdp;
using Xunit;

namespace NestProxy.Rtsp.Tests.Rtsp.Sdp;

public class SdpParserTests
{
    private readonly SdpParser _parser = new();

    [Fact]
    public void Parse_WithBasicSdp_ParsesCorrectly()
    {
        var sdp = @"v=0
o=- 123 456 IN IP4 127.0.0.1
s=Test Session
c=IN IP4 192.168.1.1
t=0 0
";

        var result = _parser.Parse(sdp);

        Assert.Equal(0, result.Version);
        Assert.Equal("- 123 456 IN IP4 127.0.0.1", result.Origin);
        Assert.Equal("Test Session", result.SessionName);
        Assert.Equal("IN IP4 192.168.1.1", result.Connection);
        Assert.Equal("0 0", result.Timing);
    }

    [Fact]
    public void Parse_WithSingleVideoMedia_ParsesMedia()
    {
        var sdp = @"v=0
o=- 123 456 IN IP4 127.0.0.1
s=Video
t=0 0
m=video 0 RTP/AVP 96
a=rtpmap:96 H264/90000
a=control:track1
";

        var result = _parser.Parse(sdp);

        Assert.Single(result.Medias);
        var media = result.Medias[0];
        Assert.Equal(MediaType.Video, media.MediaType);
        Assert.Equal(0, media.Port);
        Assert.Equal("RTP/AVP", media.Protocol);
        Assert.Equal(["96"], media.Formats);
        Assert.Equal(2, media.Attributes.Count);
    }

    [Fact]
    public void Parse_WithMultipleMedia_ParsesAll()
    {
        var sdp = @"v=0
o=- 123 456 IN IP4 127.0.0.1
s=Audio and Video
t=0 0
m=audio 0 RTP/AVP 97
a=rtpmap:97 MPEG4-GENERIC/16000/1
a=control:trackID=1
m=video 0 RTP/AVP 96
a=rtpmap:96 H264/90000
a=control:trackID=2
";

        var result = _parser.Parse(sdp);

        Assert.Equal(2, result.Medias.Count);
        Assert.Equal(MediaType.Audio, result.Medias[0].MediaType);
        Assert.Equal(MediaType.Video, result.Medias[1].MediaType);
    }

    [Fact]
    public void Parse_WithSessionAttributes_ParsesAttributes()
    {
        var sdp = @"v=0
o=- 123 456 IN IP4 127.0.0.1
s=Test
t=0 0
a=control:*
a=range:npt=0-
";

        var result = _parser.Parse(sdp);

        Assert.Equal(2, result.Attributes.Count);
        Assert.Equal("*", result.GetAttributeValue("control"));
        Assert.Equal("npt=0-", result.GetAttributeValue("range"));
    }

    [Fact]
    public void Parse_WithMediaAttributes_ParsesCorrectly()
    {
        var sdp = @"v=0
o=- 123 456 IN IP4 127.0.0.1
s=Test
t=0 0
m=video 0 RTP/AVP 96
a=rtpmap:96 H264/90000
a=fmtp:96 packetization-mode=1
a=control:track1
";

        var result = _parser.Parse(sdp);

        var media = result.Medias[0];
        Assert.Equal("96 H264/90000", media.GetAttributeValue("rtpmap"));
        Assert.Equal("96 packetization-mode=1", media.GetAttributeValue("fmtp"));
        Assert.Equal("track1", media.GetAttributeValue("control"));
    }

    [Fact]
    public void Parse_WithGoogleNestLikeSdp_ParsesCorrectly()
    {
        var sdp = @"v=0
o=- 2890844526 2890842807 IN IP4 10.47.16.5
s=LIVE555 Streaming Media v2018.08.28
i=RTSP/RTP stream from Nest camera
t=0 0
a=tool:LIVE555 Streaming Media v2018.08.28
a=type:broadcast
a=control:*
a=range:npt=0-
m=video 0 RTP/AVP 96
c=IN IP4 0.0.0.0
a=rtpmap:96 H264/90000
a=fmtp:96 packetization-mode=1;profile-level-id=64001F;sprop-parameter-sets=Z2QAH6zZQPAR,aOvssiw=
a=control:track1
m=audio 0 RTP/AVP 97
c=IN IP4 0.0.0.0
a=rtpmap:97 MPEG4-GENERIC/16000/1
a=fmtp:97 streamtype=5;profile-level-id=1;mode=AAC-hbr;sizelength=13;indexlength=3;indexdeltalength=3;config=1408
a=control:track2
";

        var result = _parser.Parse(sdp);

        Assert.Equal(0, result.Version);
        Assert.Contains("2890844526", result.Origin);
        Assert.Equal("LIVE555 Streaming Media v2018.08.28", result.SessionName);
        Assert.Equal("RTSP/RTP stream from Nest camera", result.SessionDescription);
        Assert.Equal("*", result.GetAttributeValue("control"));
        Assert.Equal("npt=0-", result.GetAttributeValue("range"));

        Assert.Equal(2, result.Medias.Count);

        var video = result.Medias[0];
        Assert.Equal(MediaType.Video, video.MediaType);
        Assert.Equal("96", video.Formats[0]);
        Assert.Equal("96 H264/90000", video.GetAttributeValue("rtpmap"));
        Assert.Equal("track1", video.GetAttributeValue("control"));

        var audio = result.Medias[1];
        Assert.Equal(MediaType.Audio, audio.MediaType);
        Assert.Equal("97", audio.Formats[0]);
        Assert.Equal("97 MPEG4-GENERIC/16000/1", audio.GetAttributeValue("rtpmap"));
        Assert.Equal("track2", audio.GetAttributeValue("control"));
    }

    [Fact]
    public void Parse_WithFlagAttributes_ParsesWithNullValue()
    {
        var sdp = @"v=0
o=- 123 456 IN IP4 127.0.0.1
s=Test
t=0 0
m=video 0 RTP/AVP 96
a=recvonly
a=rtpmap:96 H264/90000
";

        var result = _parser.Parse(sdp);

        var media = result.Medias[0];
        var recvonly = media.Attributes.FirstOrDefault(a => a.Key == "recvonly");
        Assert.NotNull(recvonly);
        Assert.Null(recvonly.Value);
    }

    [Fact]
    public void Parse_WithEmptyLines_IgnoresEmptyLines()
    {
        var sdp = @"v=0

o=- 123 456 IN IP4 127.0.0.1

s=Test
t=0 0

m=video 0 RTP/AVP 96
";

        var result = _parser.Parse(sdp);

        Assert.Equal(0, result.Version);
        Assert.Single(result.Medias);
    }

    [Fact]
    public void Parse_WithMultipleFormats_ParsesAllFormats()
    {
        var sdp = @"v=0
o=- 123 456 IN IP4 127.0.0.1
s=Test
t=0 0
m=video 0 RTP/AVP 96 97 98
";

        var result = _parser.Parse(sdp);

        var media = result.Medias[0];
        Assert.Equal(3, media.Formats.Count);
        Assert.Equal("96", media.Formats[0]);
        Assert.Equal("97", media.Formats[1]);
        Assert.Equal("98", media.Formats[2]);
    }

    [Fact]
    public void Parse_GetAttributeValue_IsCaseInsensitive()
    {
        var sdp = @"v=0
o=- 123 456 IN IP4 127.0.0.1
s=Test
t=0 0
a=Control:*
m=video 0 RTP/AVP 96
a=RtpMap:96 H264/90000
";

        var result = _parser.Parse(sdp);

        Assert.Equal("*", result.GetAttributeValue("control"));
        Assert.Equal("*", result.GetAttributeValue("CONTROL"));
        var media = result.Medias[0];
        Assert.Equal("96 H264/90000", media.GetAttributeValue("rtpmap"));
        Assert.Equal("96 H264/90000", media.GetAttributeValue("RTPMAP"));
    }

    [Fact]
    public void Parse_WithApplicationMedia_ParsesCorrectly()
    {
        var sdp = @"v=0
o=- 123 456 IN IP4 127.0.0.1
s=Test
t=0 0
m=application 9 TCP/RTP/AVP 96
";

        var result = _parser.Parse(sdp);

        var media = result.Medias[0];
        Assert.Equal(MediaType.Application, media.MediaType);
        Assert.Equal(9, media.Port);
        Assert.Equal("TCP/RTP/AVP", media.Protocol);
    }

    [Fact]
    public void Parse_WithTextReader_WorksCorrectly()
    {
        var sdp = "v=0\r\no=- 123 456 IN IP4 127.0.0.1\r\ns=Test\r\nt=0 0\r\n";
        using var reader = new StringReader(sdp);

        var result = _parser.Parse(reader);

        Assert.Equal(0, result.Version);
        Assert.Equal("Test", result.SessionName);
    }
}
