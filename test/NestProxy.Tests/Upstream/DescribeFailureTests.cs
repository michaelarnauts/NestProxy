using System.Text;

using Microsoft.Extensions.Logging.Abstractions;

using NestProxy.Rtsp.Client;
using NestProxy.Rtsp.Messages;
using NestProxy.Server.Streaming.Downstream.Server.Dispatching;
using NestProxy.Server.Streaming.Upstream;
using NestProxy.Server.Streaming.Upstream.Sdp;

using Xunit;

namespace NestProxy.Tests.Upstream;

public class DescribeFailureTests
{
    private const string Sdp =
        "v=0\r\n" +
        "o=- 1 1 IN IP4 127.0.0.1\r\n" +
        "s=SDM\r\n" +
        "t=0 0\r\n" +
        "a=control:*\r\n" +
        "m=video 0 RTP/AVP 97\r\n" +
        "a=rtpmap:97 H264/90000\r\n" +
        "a=control:trackID=1\r\n";

    private static readonly Uri StreamUri = new("rtsps://stream.example.com:443/sdm_live_stream/abc?auth=token");

    private static RtspResponse DescribeResponse(int code, string phrase, string body)
    {
        RtspResponse response = RtspRequest.CreateDescribe(StreamUri).CreateResponse(code, phrase);
        response.Headers["Content-Base"] = StreamUri + "/";
        response.Data = Encoding.UTF8.GetBytes(body);
        return response;
    }

    private static async Task<(List<SdpResolutionResult> resolved, UpstreamSessionStateMachine machine)> HandleAsync(RtspResponse response)
    {
        var machine = new UpstreamSessionStateMachine("cam", "secondary", NullLogger.Instance);
        var resolved = new List<SdpResolutionResult>();
        machine.DescribeResolved += resolved.Add;

        await using var connection = new RtspConnection(new SessionManagerTests.FakeRtspTransportAccessor().Transport);
        await machine.HandleResponseAsync(connection, response, new RtspResponseFormatter());
        return (resolved, machine);
    }

    [Fact]
    public async Task Describe404_DoesNotPublishSdpOrSetup()
    {
        // Google answers a refresh with 404 while the active session keeps streaming (seen 2026-10-19 and 2026-11-14).
        (List<SdpResolutionResult> resolved, UpstreamSessionStateMachine machine) = await HandleAsync(DescribeResponse(404, "Not Found", string.Empty));

        Assert.Empty(resolved);
        Assert.Empty(machine.InterleavedAssignments);
    }

    [Fact]
    public async Task Describe200_PublishesSdpAndStartsSetup()
    {
        (List<SdpResolutionResult> resolved, UpstreamSessionStateMachine machine) = await HandleAsync(DescribeResponse(200, "OK", Sdp));

        SdpResolutionResult result = Assert.Single(resolved);
        Assert.NotEmpty(result.SdpLines);
        Assert.Single(machine.InterleavedAssignments);
    }
}
