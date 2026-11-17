using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using NestProxy.Rtsp.Messages;
using NestProxy.Rtsp.Server;
using NestProxy.Server.Streaming.Downstream.Server.Dispatching;
using NestProxy.Server.Streaming.Downstream.Server.Dispatching.Handlers;
using NestProxy.Server.Streaming.Downstream.Sessions;

using Xunit;

namespace NestProxy.Tests;

public class TeardownRequestHandlerTests
{
    private static readonly Uri CameraUri = new("rtsp://nestproxy:8600/voordeur");

    private static TeardownRequestHandler CreateHandler(Mock<ISessionRegistry> sessions)
        => new(sessions.Object, new RtspResponseFormatter(), NullLogger<TeardownRequestHandler>.Instance);

    [Fact]
    public async Task TeardownWithoutSession_RepliesBeforeConnectionIsDisposed()
    {
        // go2rtc sends TEARDOWN without a Session header after a DESCRIBE-only probe; removing the connection
        // disposes it, which used to make the reply throw ObjectDisposedException (thousands of errors per outage).
        var connection = new RtspServerConnection(new SessionManagerTests.FakeRtspTransportAccessor().Transport);
        var sessions = new Mock<ISessionRegistry>();
        sessions.Setup(s => s.RemoveSessionsForConnection(connection)).Callback(connection.Dispose);
        TeardownRequestHandler handler = CreateHandler(sessions);

        RtspRequest request = RtspRequest.CreateTeardown(CameraUri);
        request.Headers.Remove("Session");
        await handler.HandleAsync(connection, request, "[test]", TestContext.Current.CancellationToken);

        sessions.Verify(s => s.RemoveSessionsForConnection(connection), Times.Once);
    }

    [Fact]
    public async Task TeardownWithSession_RepliesBeforeSessionIsDisposed()
    {
        var connection = new RtspServerConnection(new SessionManagerTests.FakeRtspTransportAccessor().Transport);
        var sessions = new Mock<ISessionRegistry>();
        sessions.Setup(s => s.RemoveSessionById("abc")).Callback(connection.Dispose).Returns(true);
        TeardownRequestHandler handler = CreateHandler(sessions);

        RtspRequest request = RtspRequest.CreateTeardown(CameraUri, "abc");
        await handler.HandleAsync(connection, request, "[test]", TestContext.Current.CancellationToken);

        sessions.Verify(s => s.RemoveSessionById("abc"), Times.Once);
    }
}
