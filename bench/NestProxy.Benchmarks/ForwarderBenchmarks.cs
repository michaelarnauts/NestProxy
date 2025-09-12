using System.Net;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NestProxy.Rtsp.Messages;
using NestProxy.Rtsp.Server;
using NestProxy.Rtsp.Transport;
using NestProxy.Server.Core;
using NestProxy.Server.Models;
using NestProxy.Server.Streaming.Downstream.Sessions;
using NestProxy.Server.Streaming.Forwarding;
using NestProxy.Server.Streaming.Forwarding.Binding;

namespace NestProxy.Benchmarks;

public class ForwarderBenchmarks
{
    private readonly StreamForwarder _forwarder;
    private readonly CameraStream _camera;
    private readonly ClientSession _session;
    private readonly byte[] _rtpAudio;
    private readonly byte[] _rtpVideo;

    public ForwarderBenchmarks()
    {
        var cameraRegistry = new CameraRegistry();
        _camera = new CameraStream { CameraId = "cam-1", VideoPayloadType = 97, Sdp = ["v=0"] };
        cameraRegistry[_camera.CameraId] = _camera;
        var selector = new BindingSelector();
        var options = Options.Create(new StreamingOptions { StatsWindowSeconds = 5 });
        var stats = new StatisticsReporter(cameraRegistry, options, new NullLogger<StatisticsReporter>());
        var sessionManager = new SessionRegistry(cameraRegistry, options, new NullLogger<SessionRegistry>());
        _forwarder = new StreamForwarder(cameraRegistry, sessionManager, selector, stats, new RtpPacketParser(), new NullLogger<StreamForwarder>());

        var connection = new RtspServerConnection(new NoopRtspTransport());
        _session = sessionManager.GetOrCreateSession(_camera, connection);
        _session.IsPlaying = true;
        var binding = new TrackBinding { Transport = new BenchmarkRtpTransport() };
        _session.BindingsByPayload[_camera.VideoPayloadType] = [binding];

        _rtpAudio = BuildRtp(0x01020304, _camera.VideoPayloadType, 1, 90000, 160);
        _rtpVideo = BuildRtp(0x01020305, _camera.VideoPayloadType, 2, 90090, 1372);
    }

    private static byte[] BuildRtp(uint ssrc, int pt, ushort seq, uint ts, int payloadBytes)
    {
        byte[] buf = new byte[12 + payloadBytes];
        buf[0] = 0x80;
        buf[1] = (byte)(pt & 0x7F);
        buf[2] = (byte)(seq >> 8);
        buf[3] = (byte)seq;
        buf[4] = (byte)(ts >> 24);
        buf[5] = (byte)(ts >> 16);
        buf[6] = (byte)(ts >> 8);
        buf[7] = (byte)ts;
        buf[8] = (byte)(ssrc >> 24);
        buf[9] = (byte)(ssrc >> 16);
        buf[10] = (byte)(ssrc >> 8);
        buf[11] = (byte)ssrc;
        return buf;
    }

    [Benchmark]
    public async Task ForwardAudioRtp()
    {
        await _forwarder.ForwardRtpAsync(_camera.CameraId, _rtpAudio);
    }

    [Benchmark]
    public async Task ForwardVideoRtp()
    {
        await _forwarder.ForwardRtpAsync(_camera.CameraId, _rtpVideo);
    }

    private sealed class BenchmarkRtpTransport : IRtpTransport
    {
        public Task SendFrameAsync(int channel, ReadOnlyMemory<byte> frame, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task WriteToDataPortAsync(ReadOnlyMemory<byte> packet) => Task.CompletedTask;
        public Task WriteToControlPortAsync(ReadOnlyMemory<byte> packet) => Task.CompletedTask;
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class NoopRtspTransport : IRtspTransport
    {
        public bool Connected => true;
        public string RemoteAddress => "127.0.0.1";
        public IPEndPoint RemoteEndPoint => new(IPAddress.Loopback, 8554);
        public IPEndPoint LocalEndPoint => new(IPAddress.Loopback, 8554);
        public uint NextCommandIndex() => 1;
        public void Reconnect() { }
        public void Close() { }
        public Stream GetStream() => Stream.Null;
        public Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<RtspResponse> SendRequestAsync(RtspRequest request, CancellationToken cancellationToken) => Task.FromResult(request.CreateResponse());
        public Task SendResponseAsync(RtspResponse response, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<object> ReceiveMessageAsync(CancellationToken cancellationToken) => Task.FromResult<object>(new RtspResponse());
        public Task<byte> PeekByteAsync(CancellationToken cancellationToken) => Task.FromResult((byte)0);
        public Task<InterleavedData> ReceiveInterleavedDataAsync(CancellationToken cancellationToken) => Task.FromResult(new InterleavedData { Channel = 0, Data = [] });
        public Task SendInterleavedDataAsync(byte channel, ReadOnlyMemory<byte> data, CancellationToken cancellationToken) => Task.CompletedTask;
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
