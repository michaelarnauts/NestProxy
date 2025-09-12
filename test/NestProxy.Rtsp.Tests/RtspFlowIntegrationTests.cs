using System.Net;

using FluentAssertions;
using NestProxy.Rtsp.Client;
using NestProxy.Rtsp.Messages;
using NestProxy.Rtsp.Transport;
using Xunit;

namespace NestProxy.Rtsp.Tests;

public class RtspFlowIntegrationTests
{
    [Fact]
    public async Task BasicPlayLifecycle_SequencesRequestsAndHeaders()
    {
        var transport = new RecordingRtspTransport();
        var client = new RtspClient(transport);
        var baseUri = new Uri("rtsp://example.com/stream");
        var trackUri = new Uri(baseUri, "trackID=0");

        transport.EnqueueResponse(BuildResponse());
        transport.EnqueueResponse(BuildResponse(headers: new Dictionary<string, string> { ["Content-Type"] = "application/sdp", ["Content-Length"] = "0" }));
        transport.EnqueueResponse(BuildResponse(headers: new Dictionary<string, string> { ["Session"] = "abc123", ["Transport"] = "RTP/AVP/TCP;interleaved=0-1" }));
        transport.EnqueueResponse(BuildResponse(headers: new Dictionary<string, string> { ["Session"] = "abc123" }));
        transport.EnqueueResponse(BuildResponse(headers: new Dictionary<string, string> { ["Session"] = "abc123" }));

        var options = await client.OptionsAsync(baseUri);
        var describe = await client.DescribeAsync(baseUri);
        var setup = await client.SetupAsync(trackUri, "RTP/AVP/TCP;interleaved=0-1");
        var sessionId = setup.Headers["Session"];
        var play = await client.PlayAsync(baseUri, sessionId);
        var teardown = await client.TeardownAsync(baseUri, sessionId);

        options.StatusCode.Should().Be(RtspStatusCode.OK);
        describe.StatusCode.Should().Be(RtspStatusCode.OK);
        setup.StatusCode.Should().Be(RtspStatusCode.OK);
        play.StatusCode.Should().Be(RtspStatusCode.OK);
        teardown.StatusCode.Should().Be(RtspStatusCode.OK);

        transport.SentRequests.Select(r => r.Method).Should().ContainInOrder(
            RtspMethod.OPTIONS,
            RtspMethod.DESCRIBE,
            RtspMethod.SETUP,
            RtspMethod.PLAY,
            RtspMethod.TEARDOWN);

        transport.SentRequests.Select(r => r.CSeq).Should().ContainInOrder(1, 2, 3, 4, 5);
        transport.SentRequests[1].Headers["Accept"].Should().Be("application/sdp");
        transport.SentRequests[2].Headers["Transport"].Should().Be("RTP/AVP/TCP;interleaved=0-1");
        transport.SentRequests[3].Headers["Session"].Should().Be(sessionId);
        transport.SentRequests[4].Headers["Session"].Should().Be(sessionId);
    }

    [Fact]
    public async Task KeepAliveAndPause_RoundTripsSessionHeaders()
    {
        var transport = new RecordingRtspTransport();
        var client = new RtspClient(transport);
        var uri = new Uri("rtsp://example.com/live");

        transport.EnqueueResponse(BuildResponse(headers: new Dictionary<string, string> { ["Session"] = "sess-001" }));
        transport.EnqueueResponse(BuildResponse(headers: new Dictionary<string, string> { ["Session"] = "sess-001" }));
        transport.EnqueueResponse(BuildResponse(headers: new Dictionary<string, string> { ["Session"] = "sess-001" }));

        var setup = await client.SetupAsync(new Uri(uri, "trackID=1"), "RTP/AVP/TCP;interleaved=0-1");
        var sessionId = setup.Headers["Session"];
        var keepAlive = await client.GetParameterAsync(uri, sessionId);
        var pause = await client.PauseAsync(uri, sessionId);

        setup.StatusCode.Should().Be(RtspStatusCode.OK);
        keepAlive.StatusCode.Should().Be(RtspStatusCode.OK);
        pause.StatusCode.Should().Be(RtspStatusCode.OK);

        transport.SentRequests.Select(r => r.Method).Should().ContainInOrder(RtspMethod.SETUP, RtspMethod.GET_PARAMETER, RtspMethod.PAUSE);
        transport.SentRequests.Skip(1).Select(r => r.Headers.TryGetValue("Session", out var value) ? value : string.Empty)
            .Should().AllBeEquivalentTo("sess-001");
    }

    private static RtspResponse BuildResponse(Dictionary<string, string>? headers = null)
    {
        var response = new RtspResponse(RtspStatusCode.OK, "OK", RtspVersion.Version1_0);
        if (headers != null)
        {
            foreach (var kvp in headers)
            {
                response.Headers[kvp.Key] = kvp.Value;
            }
        }

        return response;
    }

    private sealed class RecordingRtspTransport : IRtspTransport
    {
        private readonly Queue<RtspResponse> _responses = new();
        public List<RtspRequest> SentRequests { get; } = [];

        public bool Connected => true;
        public string RemoteAddress => "127.0.0.1";
        public IPEndPoint RemoteEndPoint => new(IPAddress.Loopback, 8554);
        public IPEndPoint LocalEndPoint => new(IPAddress.Loopback, 8554);

        public void EnqueueResponse(RtspResponse response) => _responses.Enqueue(response);

        public uint NextCommandIndex() => (uint)SentRequests.Count + 1;
        public void Reconnect() { }
        public void Close() { }
        public Stream GetStream() => Stream.Null;
        public Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<RtspResponse> SendRequestAsync(RtspRequest request, CancellationToken cancellationToken)
        {
            SentRequests.Add(request);
            if (!_responses.TryDequeue(out RtspResponse? response))
            {
                throw new InvalidOperationException("No response queued for request.");
            }

            if (response.CSeq == 0)
            {
                response.CSeq = request.CSeq;
                response.Headers["CSeq"] = request.CSeq.ToString();
            }

            if (request.Headers.TryGetValue("Session", out string? session) && !response.Headers.ContainsKey("Session"))
            {
                response.Headers["Session"] = session;
            }

            return Task.FromResult(response);
        }

        public Task SendResponseAsync(RtspResponse response, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<object> ReceiveMessageAsync(CancellationToken cancellationToken) => Task.FromResult<object>(new RtspResponse());
        public Task<byte> PeekByteAsync(CancellationToken cancellationToken) => Task.FromResult((byte)0);
        public Task<InterleavedData> ReceiveInterleavedDataAsync(CancellationToken cancellationToken) => Task.FromResult(new InterleavedData { Channel = 0, Data = [] });
        public Task SendInterleavedDataAsync(byte channel, ReadOnlyMemory<byte> data, CancellationToken cancellationToken) => Task.CompletedTask;
        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
