using System.Net;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NestProxy.Rtsp.Messages;
using NestProxy.Rtsp.Server;
using NestProxy.Rtsp.Transport;
using NestProxy.Server.Core;
using NestProxy.Server.Models;
using NestProxy.Server.Streaming.Downstream.Sessions;

using Xunit;

namespace NestProxy.Tests;

public class SessionManagerTests
{
    private static (SessionRegistry mgr, CameraRegistry cams, CameraStream cam, RtspServerConnection connection) Create()
    {
        CameraRegistry cameras = new();
        CameraStream camera = new() { CameraId = "cam1" };
        cameras[camera.CameraId] = camera;
        var options = Options.Create(new StreamingOptions { SessionTimeoutSeconds = 1 });
        SessionRegistry mgr = new(cameras, options, NullLogger<SessionRegistry>.Instance);
        FakeRtspTransport transport = new();
        RtspServerConnection connection = new(transport);
        return (mgr, cameras, camera, connection);
    }

    [Fact]
    public void GetOrCreateSession_CreatesOnce()
    {
        var (mgr, _, cam, connection) = Create();
        ClientSession s1 = mgr.GetOrCreateSession(cam, connection);
        ClientSession s2 = mgr.GetOrCreateSession(cam, connection);
        Assert.Same(s1, s2);
        Assert.Single(cam.Sessions);
    }

    [Fact]
    public async Task Sweep_RemovesExpired()
    {
        var (mgr, _, cam, connection) = Create();
        ClientSession s1 = mgr.GetOrCreateSession(cam, connection);
        s1.LastActivityUtc = DateTime.UtcNow - TimeSpan.FromSeconds(5);

        await Task.Delay(60, TestContext.Current.CancellationToken);

        int removed = mgr.SweepExpiredSessions();
        Assert.Equal(1, removed);
        Assert.Empty(cam.Sessions);
    }

    [Fact]
    public void GetSessionsSnapshot_ReturnsIndependentCopy()
    {
        var (mgr, _, cam, connection) = Create();
        ClientSession s1 = mgr.GetOrCreateSession(cam, connection);
        ClientSession s2 = mgr.GetOrCreateSession(cam, new RtspServerConnection(new FakeRtspTransport()));

        IReadOnlyList<ClientSession> snapshot = mgr.GetSessionsSnapshot(cam.CameraId);
        Assert.Equal(2, snapshot.Count);

        // Removing a session should not affect the snapshot
        mgr.RemoveSession(s1);
        Assert.Equal(2, snapshot.Count);
        Assert.Single(cam.Sessions);
    }

    private sealed class FakeRtspTransport : IRtspTransport
    {
        public bool Connected { get; set; } = true;
        public IPEndPoint RemoteEndPoint => new(IPAddress.Loopback, 0);
        public IPEndPoint LocalEndPoint => new(IPAddress.Loopback, 0);
        public string RemoteAddress => RemoteEndPoint.Address.ToString();
        public uint NextCommandIndex() => 0u;
        public void Reconnect() { }
        public void Close() { }
        public Stream GetStream() => Stream.Null;
        public Task WriteAsync(byte[] _, int __, int ___, CancellationToken ____) => Task.CompletedTask;
        public Task WriteAsync(ReadOnlyMemory<byte> _, CancellationToken __) => Task.CompletedTask;
        public Task<RtspResponse> SendRequestAsync(RtspRequest request, CancellationToken cancellationToken) => Task.FromResult(request.CreateResponse());
        public Task SendResponseAsync(RtspResponse _, CancellationToken __) => Task.CompletedTask;
        public Task<object> ReceiveMessageAsync(CancellationToken _) => Task.FromResult<object>(new RtspResponse());
        public Task<byte> PeekByteAsync(CancellationToken _) => Task.FromResult((byte)0);
        public Task<InterleavedData> ReceiveInterleavedDataAsync(CancellationToken _) => Task.FromResult(new InterleavedData { Channel = 0, Data = [] });
        public Task SendInterleavedDataAsync(byte _, ReadOnlyMemory<byte> __, CancellationToken ___) => Task.CompletedTask;
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    // Accessor for other test classes needing a trivial IRtspTransport
    public sealed class FakeRtspTransportAccessor
    {
        public IRtspTransport Transport { get; } = new FakeRtspTransport();
    }
}
