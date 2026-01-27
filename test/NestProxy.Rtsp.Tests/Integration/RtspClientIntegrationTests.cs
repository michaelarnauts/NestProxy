using NestProxy.Rtsp.Client;
using NestProxy.Rtsp.Messages;
using NestProxy.Rtsp.Server;
using NestProxy.Rtsp.Transport;

using Xunit;

namespace NestProxy.Rtsp.Tests.Integration;

public sealed class RtspClientIntegrationTests
{
    [Fact]
    public async Task FullRtspFlow_RoundsTripsThroughServer()
    {
        await using TestServer server = new();
        server.RegisterDefaultHandlers();
        await server.StartAsync();

        using RtspClient client = new(new RtspTcpTransport("127.0.0.1", server.Port));
        Uri uri = new($"rtsp://127.0.0.1:{server.Port}/media");

        RtspResponse options = await client.OptionsAsync(uri, TestContext.Current.CancellationToken);
        Assert.Equal(200, options.ReturnCode);
        Assert.Equal("OPTIONS, DESCRIBE, SETUP, PLAY, PAUSE, GET_PARAMETER, TEARDOWN", options.Headers["Public"]);

        RtspResponse describe = await client.DescribeAsync(uri, TestContext.Current.CancellationToken);
        Assert.Equal(200, describe.ReturnCode);
        Assert.Equal("application/sdp", describe.ContentType);
        Assert.Contains("m=video", describe.Body);

        RtspResponse setup = await client.SetupAsync(new Uri(uri, "trackID=1"), "RTP/AVP/TCP;interleaved=0-1", cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(200, setup.ReturnCode);
        Assert.Equal("12345678", setup.Session);
        Assert.Equal("RTP/AVP/TCP;interleaved=0-1", setup.Headers["Transport"]);

        RtspResponse play = await client.PlayAsync(uri, setup.Session!, TestContext.Current.CancellationToken);
        Assert.Equal(200, play.ReturnCode);
        Assert.Equal("npt=0-", play.Headers["Range"]);

        RtspResponse pause = await client.PauseAsync(uri, setup.Session!, TestContext.Current.CancellationToken);
        Assert.Equal(200, pause.ReturnCode);

        RtspResponse getParameter = await client.GetParameterAsync(uri, setup.Session, TestContext.Current.CancellationToken);
        Assert.Equal(200, getParameter.ReturnCode);
        Assert.Equal(setup.Session, getParameter.Session);

        RtspResponse teardown = await client.TeardownAsync(uri, setup.Session!, TestContext.Current.CancellationToken);
        Assert.Equal(200, teardown.ReturnCode);

        Assert.Equal(["OPTIONS", "DESCRIBE", "SETUP", "PLAY", "PAUSE", "GET_PARAMETER", "TEARDOWN"], server.ReceivedMethods);
        Assert.True(server.ReceivedCSeqs.SequenceEqual(server.ReceivedCSeqs.OrderBy(x => x)), "CSeq values should be monotonically increasing");
    }

    [Fact]
    public async Task Client_SendCustomRequest_UsesNextCSeqAndReceivesResponse()
    {
        await using TestServer server = new();
        server.RegisterDefaultHandlers();
        await server.StartAsync();

        using RtspClient client = new(new RtspTcpTransport("127.0.0.1", server.Port));
        Uri uri = new($"rtsp://127.0.0.1:{server.Port}/media");

        RtspRequest request = new(RtspMethod.OPTIONS, uri, RtspVersion.Version1_0);
        RtspResponse response = await client.SendRequestAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(200, response.ReturnCode);
        Assert.True(response.CSeq > 0);
        Assert.Contains(response.CSeq, server.ReceivedCSeqs);
    }

    private sealed class TestServer : IAsyncDisposable
    {
        private readonly RtspServer _server;
        private readonly CancellationTokenSource _cts = new();
        private readonly Dictionary<string, Func<RtspRequest, CancellationToken, Task<RtspResponse>>> _handlers = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<int> _receivedCSeqs = [];
        private readonly List<string> _receivedMethods = [];

        public TestServer()
        {
            _server = new RtspServer(0);
            _server.ClientConnected += OnClientConnected;
        }

        public int Port => _server.Port;
        public IReadOnlyList<int> ReceivedCSeqs => _receivedCSeqs;
        public IReadOnlyList<string> ReceivedMethods => _receivedMethods;

        public void RegisterDefaultHandlers()
        {
            _handlers["OPTIONS"] = (req, _) => Task.FromResult(CreateBasicResponse(req, headers => headers["Public"] = "OPTIONS, DESCRIBE, SETUP, PLAY, PAUSE, GET_PARAMETER, TEARDOWN"));
            _handlers["DESCRIBE"] = (req, _) => Task.FromResult(CreateDescribeResponse(req));
            _handlers["SETUP"] = (req, _) => Task.FromResult(CreateSetupResponse(req));
            _handlers["PLAY"] = (req, _) => Task.FromResult(CreateResponseWithHeader(req, "Range", "npt=0-"));
            _handlers["PAUSE"] = (req, _) => Task.FromResult(req.CreateResponse());
            _handlers["GET_PARAMETER"] = (req, _) => Task.FromResult(CreateResponseWithHeader(req, "Session", req.Session ?? string.Empty));
            _handlers["TEARDOWN"] = (req, _) => Task.FromResult(req.CreateResponse());
        }

        public async Task StartAsync() => await _server.StartAsync();

        private void OnClientConnected(object? _, RtspServerConnection connection)
        {
            connection.Start();
            _ = Task.Run(async () => await ProcessConnectionAsync(connection, _cts.Token));
        }

        private async Task ProcessConnectionAsync(RtspServerConnection connection, CancellationToken cancellationToken)
        {
            await foreach (RtspMessage message in connection.Messages.ReadAllAsync(cancellationToken))
            {
                if (message is not RtspRequest request)
                {
                    continue;
                }

                _receivedMethods.Add(request.Method);
                _receivedCSeqs.Add(request.CSeq);

                if (_handlers.TryGetValue(request.Method, out Func<RtspRequest, CancellationToken, Task<RtspResponse>>? handler))
                {
                    RtspResponse response = await handler(request, cancellationToken);
                    await connection.SendResponseAsync(response, cancellationToken);
                }
                else
                {
                    RtspResponse notAllowed = request.CreateResponse(405, "Method Not Allowed");
                    await connection.SendResponseAsync(notAllowed, cancellationToken);
                }
            }
        }

        private static RtspResponse CreateBasicResponse(RtspRequest request, Action<Dictionary<string, string>> configure)
        {
            RtspResponse response = request.CreateResponse();
            configure(response.Headers);
            return response;
        }

        private static RtspResponse CreateDescribeResponse(RtspRequest request)
        {
            const string sdp = "v=0\r\no=- 0 0 IN IP4 127.0.0.1\r\ns=NestProxy\r\nm=video 0 RTP/AVP 96\r\nc=IN IP4 0.0.0.0\r\na=control:trackID=1\r\na=rtpmap:96 H264/90000\r\n";
            RtspResponse response = request.CreateResponse();
            response.ContentType = "application/sdp";
            response.SetBodyFromString(sdp);
            return response;
        }

        private static RtspResponse CreateSetupResponse(RtspRequest request)
        {
            RtspResponse response = request.CreateResponse();
            response.Session = "12345678";
            if (request.Headers.TryGetValue("Transport", out string? transport))
            {
                response.Headers["Transport"] = transport;
            }

            return response;
        }

        private static RtspResponse CreateResponseWithHeader(RtspRequest request, string header, string value)
        {
            RtspResponse response = request.CreateResponse();
            response.Headers[header] = value;
            return response;
        }

        public async ValueTask DisposeAsync()
        {
            _cts.Cancel();
            await _server.DisposeAsync();
            _cts.Dispose();
        }
    }
}
