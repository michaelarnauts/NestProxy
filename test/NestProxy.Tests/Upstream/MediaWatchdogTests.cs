using System.Net.Security;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NestProxy.Server.Core;
using NestProxy.Server.Models;
using NestProxy.Server.Streaming.Downstream.Server;
using NestProxy.Server.Streaming.Forwarding.Binding;
using NestProxy.Server.Streaming.Upstream;
using NestProxy.Server.Streaming.Upstream.Sdp;

using Xunit;

namespace NestProxy.Tests.Upstream;

public class MediaWatchdogTests
{
    private sealed class ManualTimeProvider : TimeProvider
    {
        private long _ticks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => _ticks;
        public void Advance(TimeSpan by) => _ticks += by.Ticks;
    }

    private sealed class TestSession(string instanceId) : IUpstreamSession
    {
        private Func<ReadOnlyMemory<byte>, ValueTask>? _onRtp;

        public string InstanceId => instanceId;
        public string? SessionId => "session";
        public string? BaseUrl => "rtsps://example.com/stream?auth=old";
        public string? AggregateUrl => null;
        public IReadOnlyList<(int data, int control)> InterleavedAssignments => Array.Empty<(int, int)>();
        public uint? LastObservedRtpSsrc => null;
        public bool ByeObserved => false;
        public event Action<SdpResolutionResult>? DescribeResolved { add { } remove { } }
        public event Action? Disconnected { add { } remove { } }
        public ValueTask ReceiveRtpAsync() => _onRtp!.Invoke(new byte[12]);
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public Task ConnectAsync(Uri url, Func<ReadOnlyMemory<byte>, ValueTask> onRtp, Func<ReadOnlyMemory<byte>, ValueTask> onRtcp, CancellationToken ct)
        {
            _onRtp = onRtp;
            return Task.CompletedTask;
        }
        public Task TeardownAsync() => Task.CompletedTask;
        public Task CloseAsync() => Task.CompletedTask;
        public bool TryGetFirstControlChannel(out int controlChannel) { controlChannel = 1; return true; }
        public void SendOptionsKeepalive() { }
        public void SendGetParameterKeepalive() { }
        public void SendRtcpKeepalive(byte[] packet) { }
        public void SendRtcpKeepalive(byte[] packet, int length) { }
    }

    private sealed class TestSessionFactory : IUpstreamSessionFactory
    {
        public TestSession Primary { get; } = new("primary");
        public TestSession Secondary { get; } = new("secondary");

        public IUpstreamSession Create(string cameraId, RemoteCertificateValidationCallback certValidator, string instanceId)
            => instanceId == "primary" ? Primary : Secondary;
    }

    private sealed class NullLocalServer : ILocalRtspServer
    {
        public void RegisterOrUpdateCamera(CameraStream stream) { }
        public void ForwardRtp(string path, ReadOnlyMemory<byte> rtpPacket) { }
        public ValueTask ForwardRtpAsync(string cameraPath, ReadOnlyMemory<byte> rtpPacket, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public void ForwardRtcp(string path, ReadOnlyMemory<byte> rtcpPacket) { }
        public ValueTask ForwardRtcpAsync(string cameraPath, ReadOnlyMemory<byte> rtcpPacket, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public string BuildPublicUrl(string host, int port, string path) => $"rtsp://{host}:{port}/{path}";
        public string? DescribeForTest(string cameraPath) => null;
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed record Setup(UpstreamOrchestrator Orchestrator, TestSessionFactory Sessions, ManualTimeProvider Clock)
    {
        public int Raised { get; set; }
    }

    private static async Task<Setup> StartAsync()
    {
        TestSessionFactory sessions = new();
        ManualTimeProvider clock = new();
        IOptions<StreamingOptions> options = Options.Create(new StreamingOptions { KeepaliveEnabled = false, UpstreamMediaTimeoutSeconds = 15 });
        UpstreamOrchestrator orchestrator = new("cam", NullLogger.Instance, new NullLocalServer(), new BindingSelector(), options, sessions, clock);
        Setup setup = new(orchestrator, sessions, clock);
        orchestrator.Disconnected += () => setup.Raised++;
        await orchestrator.StartAsync("rtsps://example.com/stream?auth=old", TestContext.Current.CancellationToken);
        return setup;
    }

    [Fact]
    public async Task NoMediaAfterStart_RaisesDisconnectedOnceAfterTimeout()
    {
        Setup s = await StartAsync();

        s.Clock.Advance(TimeSpan.FromSeconds(14));
        Assert.False(s.Orchestrator.CheckMediaTimeout());

        s.Clock.Advance(TimeSpan.FromSeconds(1));
        Assert.True(s.Orchestrator.CheckMediaTimeout());
        Assert.False(s.Orchestrator.CheckMediaTimeout());
        Assert.Equal(1, s.Raised);
        Assert.False(s.Orchestrator.HasReceivedMedia);
    }

    [Fact]
    public async Task ActiveSessionGoesSilent_RaisesDisconnected()
    {
        // 2026-11-17: the active Nest session stopped sending RTP while its connection stayed open.
        Setup s = await StartAsync();
        await s.Sessions.Primary.ReceiveRtpAsync();
        s.Clock.Advance(TimeSpan.FromSeconds(10));
        await s.Sessions.Primary.ReceiveRtpAsync();

        s.Clock.Advance(TimeSpan.FromSeconds(14));
        Assert.False(s.Orchestrator.CheckMediaTimeout());

        s.Clock.Advance(TimeSpan.FromSeconds(1));
        Assert.True(s.Orchestrator.CheckMediaTimeout());
        Assert.Equal(1, s.Raised);
        Assert.True(s.Orchestrator.HasReceivedMedia);
    }

    [Fact]
    public async Task MediaFromPromotedSession_KeepsWatchdogQuiet()
    {
        Setup s = await StartAsync();
        await s.Sessions.Primary.ReceiveRtpAsync();
        Assert.True(await s.Orchestrator.RefreshAuthAndReplayAsync("new"));

        s.Clock.Advance(TimeSpan.FromSeconds(10));
        await s.Sessions.Secondary.ReceiveRtpAsync(); // promotes the staging session
        s.Clock.Advance(TimeSpan.FromSeconds(10));
        await s.Sessions.Secondary.ReceiveRtpAsync();
        s.Clock.Advance(TimeSpan.FromSeconds(10));

        Assert.False(s.Orchestrator.CheckMediaTimeout());
        Assert.Equal(0, s.Raised);
    }

    [Fact]
    public async Task MediaFromUnpromotedStagingSession_DoesNotCount()
    {
        Setup s = await StartAsync();
        await s.Sessions.Primary.ReceiveRtpAsync();

        // RTP from a session that is neither active nor staging is ignored.
        Assert.True(await s.Orchestrator.RefreshAuthAndReplayAsync("new"));
        await s.Sessions.Secondary.ReceiveRtpAsync(); // promotes secondary
        s.Clock.Advance(TimeSpan.FromSeconds(10));
        await s.Sessions.Primary.ReceiveRtpAsync(); // old session, not forwarded
        s.Clock.Advance(TimeSpan.FromSeconds(5));

        Assert.True(s.Orchestrator.CheckMediaTimeout());
    }

    [Fact]
    public async Task AfterDispose_NoTimeout()
    {
        Setup s = await StartAsync();
        await s.Orchestrator.DisposeAsync();

        s.Clock.Advance(TimeSpan.FromMinutes(5));

        Assert.False(s.Orchestrator.CheckMediaTimeout());
        Assert.Equal(0, s.Raised);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 5)]
    [InlineData(2, 10)]
    [InlineData(3, 20)]
    [InlineData(4, 40)]
    [InlineData(5, 60)]
    [InlineData(50, 60)]
    public void RetryDelay_BacksOffToOneMinute(int failures, int seconds)
    {
        Assert.Equal(TimeSpan.FromSeconds(seconds), CameraProxyRunner.GetRetryDelay(failures));
    }
}
