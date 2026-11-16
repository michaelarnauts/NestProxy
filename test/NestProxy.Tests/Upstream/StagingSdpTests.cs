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

public class StagingSdpTests
{
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
        public event Action<SdpResolutionResult>? DescribeResolved;
        public event Action? Disconnected { add { } remove { } }
        public void RaiseDescribe(SdpResolutionResult result) => DescribeResolved?.Invoke(result);
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

    private sealed class RecordingLocalServer : ILocalRtspServer
    {
        public List<string[]> Registered { get; } = [];
        public void RegisterOrUpdateCamera(CameraStream stream) => Registered.Add(stream.Sdp ?? []);
        public void ForwardRtp(string path, ReadOnlyMemory<byte> rtpPacket) { }
        public ValueTask ForwardRtpAsync(string cameraPath, ReadOnlyMemory<byte> rtpPacket, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public void ForwardRtcp(string path, ReadOnlyMemory<byte> rtcpPacket) { }
        public ValueTask ForwardRtcpAsync(string cameraPath, ReadOnlyMemory<byte> rtcpPacket, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public string BuildPublicUrl(string host, int port, string path) => $"rtsp://{host}:{port}/{path}";
        public string? DescribeForTest(string cameraPath) => null;
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private static readonly string[] GoodSdp = ["v=0", "o=- 1 1 IN IP4 127.0.0.1", "s=SDM", "m=video 0 RTP/AVP 97", "a=control:trackID=2"];
    private static readonly string[] NewGoodSdp = ["v=0", "o=- 2 2 IN IP4 127.0.0.1", "s=SDM", "m=video 0 RTP/AVP 97", "a=control:trackID=2"];

    // What Google returned on 2026-11-16 01:03 UTC: 200 OK with an SDP without any media.
    private static readonly string[] TracklessSdp = ["v=0", "o=- 3 3 IN IP4 127.0.0.1", "s=SDM"];

    private static SdpResolutionResult Describe(string[] lines) => new()
    {
        BaseUrl = "rtsps://example.com/stream/",
        TrackUris = lines.Any(l => l.StartsWith("m=")) ? [new Uri("rtsps://example.com/stream/trackID=2")] : [],
        SdpLines = lines,
    };

    private static async Task<(UpstreamOrchestrator orchestrator, TestSessionFactory sessions, RecordingLocalServer server)> StartAsync()
    {
        TestSessionFactory sessions = new();
        RecordingLocalServer server = new();
        IOptions<StreamingOptions> options = Options.Create(new StreamingOptions { KeepaliveEnabled = false });
        UpstreamOrchestrator orchestrator = new("cam", NullLogger.Instance, server, new BindingSelector(), options, sessions);
        await orchestrator.StartAsync("rtsps://example.com/stream?auth=old", TestContext.Current.CancellationToken);
        sessions.Primary.RaiseDescribe(Describe(GoodSdp));
        return (orchestrator, sessions, server);
    }

    [Fact]
    public async Task InitialDescribe_RegistersSdpImmediately()
    {
        var (_, _, server) = await StartAsync();

        Assert.Equal([GoodSdp], server.Registered);
    }

    [Fact]
    public async Task RefreshWithTracklessSdp_KeepsCurrentSdp()
    {
        var (orchestrator, sessions, server) = await StartAsync();
        Assert.True(await orchestrator.RefreshAuthAndReplayAsync("new"));

        sessions.Secondary.RaiseDescribe(Describe(TracklessSdp));

        Assert.Equal([GoodSdp], server.Registered);
    }

    [Fact]
    public async Task RefreshSdp_IsRegisteredOnPromotion()
    {
        var (orchestrator, sessions, server) = await StartAsync();
        Assert.True(await orchestrator.RefreshAuthAndReplayAsync("new"));

        sessions.Secondary.RaiseDescribe(Describe(NewGoodSdp));
        Assert.Equal([GoodSdp], server.Registered);

        await sessions.Secondary.ReceiveRtpAsync();
        Assert.Equal([GoodSdp, NewGoodSdp], server.Registered);
    }

    [Fact]
    public async Task SdpFromUnpromotedRefresh_IsNotUsedByLaterRefresh()
    {
        var (orchestrator, sessions, server) = await StartAsync();
        Assert.True(await orchestrator.RefreshAuthAndReplayAsync("new"));
        sessions.Secondary.RaiseDescribe(Describe(TracklessSdp)); // never delivers RTP

        // The next refresh reuses the same session; its DESCRIBE fails (404) so no new SDP arrives.
        Assert.True(await orchestrator.RefreshAuthAndReplayAsync("newer"));
        await sessions.Secondary.ReceiveRtpAsync();

        Assert.Equal([GoodSdp], server.Registered);
    }
}
