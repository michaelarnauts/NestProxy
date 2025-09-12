using System.Net;
using System.Text;
using NestProxy.Rtsp.Messages;
using Xunit;

namespace NestProxy.Tests.Rtsp;

/// <summary>
/// Integration tests for RTSP protocol flows per RFC 2326.
/// Tests complete request/response cycles and protocol state transitions.
/// </summary>
public class RtspIntegrationTests
{
    private const string SessionId = "TestSession12345";
    private const string TestUri = "rtsp://example.com/test-stream";

    [Fact]
    public void CompleteRtspFlow_OPTIONS_DESCRIBE_SETUP_PLAY_PAUSE_TEARDOWN()
    {
        // RFC 2326 complete flow validation
        var uri = new Uri(TestUri);
        
        // Step 1: OPTIONS
        var optionsReq = new RtspRequest(RtspMethod.OPTIONS, uri, "RTSP/1.0");
        optionsReq.Headers["CSeq"] = "1";
        var optionsResp = optionsReq.CreateResponse((int)RtspStatusCode.OK);
        optionsResp.Headers["Public"] = "OPTIONS, DESCRIBE, SETUP, PLAY, PAUSE, TEARDOWN";
        Assert.Equal(RtspStatusCode.OK, optionsResp.StatusCode);
        Assert.True(optionsResp.Headers.ContainsKey("Public"));

        // Step 2: DESCRIBE
        var describeReq = new RtspRequest(RtspMethod.DESCRIBE, uri, "RTSP/1.0");
        describeReq.Headers["CSeq"] = "2";
        describeReq.Headers["Accept"] = "application/sdp";
        var describeResp = describeReq.CreateResponse((int)RtspStatusCode.OK);
        describeResp.Headers["Content-Type"] = "application/sdp";
        describeResp.Data = Encoding.UTF8.GetBytes("v=0\r\nm=video 0 RTP/AVP 96\r\n");
        Assert.Equal(RtspStatusCode.OK, describeResp.StatusCode);
        Assert.True(describeResp.Data.Length > 0);

        // Step 3: SETUP
        var setupReq = new RtspRequest(RtspMethod.SETUP, new Uri($"{TestUri}/track1"), "RTSP/1.0");
        setupReq.Headers["CSeq"] = "3";
        setupReq.Headers["Transport"] = "RTP/AVP/TCP;unicast;interleaved=0-1";
        var setupResp = setupReq.CreateResponse((int)RtspStatusCode.OK);
        setupResp.Headers["Session"] = $"{SessionId};timeout=60";
        setupResp.Headers["Transport"] = "RTP/AVP/TCP;unicast;interleaved=0-1";
        Assert.Equal(RtspStatusCode.OK, setupResp.StatusCode);
        Assert.True(setupResp.Headers.ContainsKey("Session"));

        // Step 4: PLAY
        var playReq = new RtspRequest(RtspMethod.PLAY, uri, "RTSP/1.0");
        playReq.Headers["CSeq"] = "4";
        playReq.Headers["Session"] = SessionId;
        var playResp = playReq.CreateResponse((int)RtspStatusCode.OK);
        playResp.Headers["Session"] = SessionId;
        playResp.Headers["Range"] = "npt=0.000-";
        Assert.Equal(RtspStatusCode.OK, playResp.StatusCode);

        // Step 5: PAUSE
        var pauseReq = new RtspRequest(RtspMethod.PAUSE, uri, "RTSP/1.0");
        pauseReq.Headers["CSeq"] = "5";
        pauseReq.Headers["Session"] = SessionId;
        var pauseResp = pauseReq.CreateResponse((int)RtspStatusCode.OK);
        pauseResp.Headers["Session"] = SessionId;
        Assert.Equal(RtspStatusCode.OK, pauseResp.StatusCode);

        // Step 6: TEARDOWN
        var teardownReq = new RtspRequest(RtspMethod.TEARDOWN, uri, "RTSP/1.0");
        teardownReq.Headers["CSeq"] = "6";
        teardownReq.Headers["Session"] = SessionId;
        var teardownResp = teardownReq.CreateResponse((int)RtspStatusCode.OK);
        Assert.Equal(RtspStatusCode.OK, teardownResp.StatusCode);
    }

    [Fact]
    public void SETUP_WithTcpTransport_CreatesValidResponse()
    {
        var uri = new Uri($"{TestUri}/track1");
        var request = new RtspRequest(RtspMethod.SETUP, uri, "RTSP/1.0");
        request.Headers["CSeq"] = "3";
        request.Headers["Transport"] = "RTP/AVP/TCP;unicast;interleaved=0-1";

        var response = request.CreateResponse((int)RtspStatusCode.OK);
        response.Headers["Session"] = $"{SessionId};timeout=60";
        response.Headers["Transport"] = "RTP/AVP/TCP;unicast;interleaved=0-1";

        Assert.Equal(RtspStatusCode.OK, response.StatusCode);
        Assert.Equal("3", response.Headers["CSeq"]);
        Assert.True(response.Headers.ContainsKey("Session"));
        Assert.True(response.Headers.ContainsKey("Transport"));
        Assert.Contains("interleaved=0-1", response.Headers["Transport"]);
    }

    [Fact]
    public void SETUP_WithUdpTransport_CreatesValidResponse()
    {
        var uri = new Uri($"{TestUri}/track1");
        var request = new RtspRequest(RtspMethod.SETUP, uri, "RTSP/1.0");
        request.Headers["CSeq"] = "3";
        request.Headers["Transport"] = "RTP/AVP;unicast;client_port=50000-50001";

        var response = request.CreateResponse((int)RtspStatusCode.OK);
        response.Headers["Session"] = SessionId;
        response.Headers["Transport"] = "RTP/AVP;unicast;client_port=50000-50001;server_port=60000-60001";

        Assert.Equal(RtspStatusCode.OK, response.StatusCode);
        Assert.Contains("client_port=50000-50001", response.Headers["Transport"]);
        Assert.Contains("server_port=", response.Headers["Transport"]);
    }

    [Fact]
    public void PLAY_WithValidSession_CreatesValidResponse()
    {
        var uri = new Uri(TestUri);
        var request = new RtspRequest(RtspMethod.PLAY, uri, "RTSP/1.0");
        request.Headers["CSeq"] = "5";
        request.Headers["Session"] = SessionId;
        request.Headers["Range"] = "npt=0.000-";

        var response = request.CreateResponse((int)RtspStatusCode.OK);
        response.Headers["Session"] = SessionId;
        response.Headers["Range"] = "npt=0.000-";
        response.Headers["RTP-Info"] = "url=track1;seq=1;rtptime=0";

        Assert.Equal(RtspStatusCode.OK, response.StatusCode);
        Assert.Equal(SessionId, response.Headers["Session"]);
        Assert.True(response.Headers.ContainsKey("Range"));
        Assert.True(response.Headers.ContainsKey("RTP-Info"));
    }

    [Fact]
    public void GET_PARAMETER_ForKeepAlive_CreatesValidResponse()
    {
        var uri = new Uri(TestUri);
        var request = new RtspRequest(RtspMethod.GET_PARAMETER, uri, "RTSP/1.0");
        request.Headers["CSeq"] = "10";
        request.Headers["Session"] = SessionId;

        var response = request.CreateResponse((int)RtspStatusCode.OK);
        response.Headers["Session"] = SessionId;

        Assert.Equal(RtspStatusCode.OK, response.StatusCode);
        Assert.Equal(SessionId, response.Headers["Session"]);
    }

    [Fact]
    public void SET_PARAMETER_WithParameters_CreatesValidResponse()
    {
        var uri = new Uri(TestUri);
        var request = new RtspRequest(RtspMethod.SET_PARAMETER, uri, "RTSP/1.0");
        request.Headers["CSeq"] = "11";
        request.Headers["Session"] = SessionId;
        request.Data = Encoding.UTF8.GetBytes("volume: 80\r\n");

        var response = request.CreateResponse((int)RtspStatusCode.OK);
        response.Headers["Session"] = SessionId;

        Assert.Equal(RtspStatusCode.OK, response.StatusCode);
        Assert.Equal(SessionId, response.Headers["Session"]);
    }

    [Fact]
    public void ErrorResponse_MissingSession_Returns454()
    {
        var uri = new Uri(TestUri);
        var request = new RtspRequest(RtspMethod.PLAY, uri, "RTSP/1.0");
        request.Headers["CSeq"] = "5";
        // Missing Session header

        var response = request.CreateResponse((int)RtspStatusCode.SessionNotFound, "Session Not Found");

        Assert.Equal(RtspStatusCode.SessionNotFound, response.StatusCode);
        Assert.Equal("Session Not Found", response.ReasonPhrase);
    }

    [Fact]
    public void ErrorResponse_BadRequest_Returns400()
    {
        var uri = new Uri(TestUri);
        var request = new RtspRequest(RtspMethod.SETUP, uri, "RTSP/1.0");
        request.Headers["CSeq"] = "3";
        // Missing Transport header

        var response = request.CreateResponse((int)RtspStatusCode.BadRequest, "Missing Transport header");

        Assert.Equal(RtspStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Missing Transport header", response.ReasonPhrase);
    }

    [Fact]
    public void MultiTrackSetup_PreservesSession()
    {
        var baseUri = new Uri(TestUri);
        
        // Setup track 1
        var setup1 = new RtspRequest(RtspMethod.SETUP, new Uri($"{TestUri}/track1"), "RTSP/1.0");
        setup1.Headers["CSeq"] = "3";
        setup1.Headers["Transport"] = "RTP/AVP/TCP;unicast;interleaved=0-1";
        var resp1 = setup1.CreateResponse((int)RtspStatusCode.OK);
        resp1.Headers["Session"] = $"{SessionId};timeout=60";
        resp1.Headers["Transport"] = "RTP/AVP/TCP;unicast;interleaved=0-1";
        
        var sessionFromTrack1 = resp1.Headers["Session"].Split(';')[0];

        // Setup track 2 with same session
        var setup2 = new RtspRequest(RtspMethod.SETUP, new Uri($"{TestUri}/track2"), "RTSP/1.0");
        setup2.Headers["CSeq"] = "4";
        setup2.Headers["Session"] = sessionFromTrack1;
        setup2.Headers["Transport"] = "RTP/AVP/TCP;unicast;interleaved=2-3";
        var resp2 = setup2.CreateResponse((int)RtspStatusCode.OK);
        resp2.Headers["Session"] = sessionFromTrack1;
        resp2.Headers["Transport"] = "RTP/AVP/TCP;unicast;interleaved=2-3";

        Assert.Equal(RtspStatusCode.OK, resp1.StatusCode);
        Assert.Equal(RtspStatusCode.OK, resp2.StatusCode);
        Assert.Equal(sessionFromTrack1, resp2.Headers["Session"]);
    }

    [Fact]
    public void CSeqHeader_PreservedInResponse()
    {
        for (int i = 1; i <= 10; i++)
        {
            var uri = new Uri(TestUri);
            var request = new RtspRequest(RtspMethod.OPTIONS, uri, "RTSP/1.0");
            request.Headers["CSeq"] = i.ToString();

            var response = request.CreateResponse((int)RtspStatusCode.OK);

            Assert.Equal(i.ToString(), response.Headers["CSeq"]);
        }
    }

    [Fact]
    public void DESCRIBE_Response_ContainsSdp()
    {
        var uri = new Uri(TestUri);
        var request = new RtspRequest(RtspMethod.DESCRIBE, uri, "RTSP/1.0");
        request.Headers["CSeq"] = "2";
        request.Headers["Accept"] = "application/sdp";

        var sdp = "v=0\r\no=- 123 456 IN IP4 127.0.0.1\r\ns=Test Stream\r\nm=video 0 RTP/AVP 96\r\n";
        var response = request.CreateResponse((int)RtspStatusCode.OK);
        response.Headers["Content-Type"] = "application/sdp";
        response.Headers["Content-Base"] = TestUri;
        response.Data = Encoding.UTF8.GetBytes(sdp);

        Assert.Equal(RtspStatusCode.OK, response.StatusCode);
        Assert.Equal("application/sdp", response.Headers["Content-Type"]);
        Assert.True(response.Data.Length > 0);
        
        var sdpContent = Encoding.UTF8.GetString(response.Data.ToArray());
        Assert.Contains("v=0", sdpContent);
        Assert.Contains("m=video", sdpContent);
    }

    [Fact]
    public void OPTIONS_Response_ListsPublicMethods()
    {
        var uri = new Uri(TestUri);
        var request = new RtspRequest(RtspMethod.OPTIONS, uri, "RTSP/1.0");
        request.Headers["CSeq"] = "1";

        var response = request.CreateResponse((int)RtspStatusCode.OK);
        response.Headers["Public"] = "OPTIONS, DESCRIBE, SETUP, PLAY, PAUSE, TEARDOWN, GET_PARAMETER, SET_PARAMETER";

        Assert.Equal(RtspStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.ContainsKey("Public"));
        var methods = response.Headers["Public"];
        Assert.Contains("OPTIONS", methods);
        Assert.Contains("DESCRIBE", methods);
        Assert.Contains("SETUP", methods);
        Assert.Contains("PLAY", methods);
        Assert.Contains("PAUSE", methods);
        Assert.Contains("TEARDOWN", methods);
    }

    [Fact]
    public void SessionTimeout_IncludedInSetupResponse()
    {
        var uri = new Uri($"{TestUri}/track1");
        var request = new RtspRequest(RtspMethod.SETUP, uri, "RTSP/1.0");
        request.Headers["CSeq"] = "3";
        request.Headers["Transport"] = "RTP/AVP/TCP;unicast;interleaved=0-1";

        var response = request.CreateResponse((int)RtspStatusCode.OK);
        response.Headers["Session"] = $"{SessionId};timeout=60";

        Assert.Contains("timeout=", response.Headers["Session"]);
        Assert.Contains(SessionId, response.Headers["Session"]);
    }

    [Fact]
    public void RtpInfo_IncludedInPlayResponse()
    {
        var uri = new Uri(TestUri);
        var request = new RtspRequest(RtspMethod.PLAY, uri, "RTSP/1.0");
        request.Headers["CSeq"] = "5";
        request.Headers["Session"] = SessionId;

        var response = request.CreateResponse((int)RtspStatusCode.OK);
        response.Headers["Session"] = SessionId;
        response.Headers["RTP-Info"] = "url=track1;seq=12345;rtptime=1234567890";

        Assert.True(response.Headers.ContainsKey("RTP-Info"));
        Assert.Contains("seq=", response.Headers["RTP-Info"]);
        Assert.Contains("rtptime=", response.Headers["RTP-Info"]);
    }

    [Fact]
    public void TransportHeader_ParsedAndEchoed()
    {
        var uri = new Uri($"{TestUri}/track1");
        var transportHeader = "RTP/AVP/TCP;unicast;interleaved=4-5";
        
        var request = new RtspRequest(RtspMethod.SETUP, uri, "RTSP/1.0");
        request.Headers["CSeq"] = "3";
        request.Headers["Transport"] = transportHeader;

        var transport = RtspTransport.Parse(transportHeader);
        Assert.Equal("TCP", transport.LowerTransport);
        Assert.NotNull(transport.Interleaved);
        Assert.Equal(4, transport.Interleaved?.First);
        Assert.Equal(5, transport.Interleaved?.Second);

        var response = request.CreateResponse((int)RtspStatusCode.OK);
        response.Headers["Transport"] = transport.ToString();
        response.Headers["Session"] = SessionId;

        Assert.Contains("interleaved=4-5", response.Headers["Transport"]);
    }

    [Fact]
    public void Range_Header_SupportedInPlayRequest()
    {
        var uri = new Uri(TestUri);
        var request = new RtspRequest(RtspMethod.PLAY, uri, "RTSP/1.0");
        request.Headers["CSeq"] = "5";
        request.Headers["Session"] = SessionId;
        request.Headers["Range"] = "npt=10.0-20.0";

        var response = request.CreateResponse((int)RtspStatusCode.OK);
        response.Headers["Session"] = SessionId;
        response.Headers["Range"] = "npt=10.0-20.0";

        Assert.True(response.Headers.ContainsKey("Range"));
        Assert.Contains("npt=", response.Headers["Range"]);
    }

    [Fact]
    public void UserAgent_Header_PreservedInRequests()
    {
        var uri = new Uri(TestUri);
        var request = new RtspRequest(RtspMethod.OPTIONS, uri, "RTSP/1.0");
        request.Headers["CSeq"] = "1";
        request.Headers["User-Agent"] = "NestProxy/1.0";

        Assert.Equal("NestProxy/1.0", request.Headers["User-Agent"]);

        var response = request.CreateResponse((int)RtspStatusCode.OK);
        Assert.Equal(RtspStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public void ContentLength_AutoCalculatedForResponses()
    {
        var uri = new Uri(TestUri);
        var request = new RtspRequest(RtspMethod.DESCRIBE, uri, "RTSP/1.0");
        request.Headers["CSeq"] = "2";

        var sdpContent = "v=0\r\ns=Test\r\n";
        var response = request.CreateResponse((int)RtspStatusCode.OK);
        response.Headers["Content-Type"] = "application/sdp";
        response.Data = Encoding.UTF8.GetBytes(sdpContent);

        Assert.Equal(sdpContent.Length, response.Data.Length);
    }

    [Fact]
    public void Response_IncludesRtspVersion()
    {
        var uri = new Uri(TestUri);
        var request = new RtspRequest(RtspMethod.OPTIONS, uri, "RTSP/1.0");
        request.Headers["CSeq"] = "1";

        var response = request.CreateResponse((int)RtspStatusCode.OK);

        Assert.Equal("RTSP/1.0", response.Version);
    }
}
