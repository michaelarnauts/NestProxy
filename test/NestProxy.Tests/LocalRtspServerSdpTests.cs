using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NestProxy.Rtsp.Server;
using NestProxy.Rtsp.Transport;
using NestProxy.Server.Core;
using NestProxy.Server.Models;
using NestProxy.Server.Streaming.Downstream.Server;
using NestProxy.Server.Streaming.Downstream.Server.Describe;
using NestProxy.Server.Streaming.Downstream.Server.Dispatching;
using NestProxy.Server.Streaming.Downstream.Server.Dispatching.Handlers;
using NestProxy.Server.Streaming.Downstream.Server.Transport;
using NestProxy.Server.Streaming.Downstream.Sessions;
using NestProxy.Server.Streaming.Forwarding;
using NestProxy.Server.Streaming.Forwarding.Binding;

using Xunit;

namespace NestProxy.Tests;

public class LocalRtspServerSdpTests
{
    private class TestFixture
    {
        public CameraRegistry Cameras { get; }
        public BindingSelector BindingSelector { get; }
        public IOptions<StreamingOptions> StreamingOptions { get; }
        public StatisticsReporter Stats { get; }
        public SessionRegistry SessionManager { get; }
        public ITransportAllocator Allocator { get; }
        public ControlMapBuilder ControlMap { get; }
        public StreamForwarder Forwarder { get; }
        public TransportSetupService TransportSetup { get; }
        public DummyListenerManager ListenerManager { get; }
        public LocalRtspServer Server { get; }

        public TestFixture()
        {
            Cameras = new CameraRegistry();
            BindingSelector = new BindingSelector();
            StreamingOptions = Options.Create(new StreamingOptions { StatsWindowSeconds = 5 });
            Stats = new StatisticsReporter(Cameras, StreamingOptions, new NullLogger<StatisticsReporter>());
            SessionManager = new SessionRegistry(Cameras, StreamingOptions, new NullLogger<SessionRegistry>());
            Allocator = new TransportAllocator(new NullLogger<TransportAllocator>());
            ControlMap = new ControlMapBuilder();
            Forwarder = new StreamForwarder(Cameras, SessionManager, BindingSelector, Stats, new RtpPacketParser(), NullLogger<StreamForwarder>.Instance);
            TransportSetup = new TransportSetupService(Allocator, new TransportNegotiator(Allocator), new NullLogger<TransportSetupService>());

            var describeSynthesizer = new DescribeSynthesizer(new SdpRewriter());
            var responseFormatter = new RtspResponseFormatter();
            var controlMapResolver = new ControlMapResolver(Cameras);
            var describeHandler = new DescribeRequestHandler(Cameras, describeSynthesizer, responseFormatter, NullLogger<DescribeRequestHandler>.Instance);
            var setupHandler = new SetupRequestHandler(Cameras, SessionManager, TransportSetup, controlMapResolver, responseFormatter, NullLogger<SetupRequestHandler>.Instance);
            var playHandler = new PlayRequestHandler(Cameras, responseFormatter);
            var teardownHandler = new TeardownRequestHandler(SessionManager, responseFormatter, NullLogger<TeardownRequestHandler>.Instance);
            var dispatcher = new RtspRequestDispatcher(Cameras, SessionManager, responseFormatter, describeHandler, setupHandler, playHandler, teardownHandler, NullLogger<RtspRequestDispatcher>.Instance);

            ListenerManager = new DummyListenerManager();
            Server = new LocalRtspServer(
                new NullLogger<LocalRtspServer>(),
                StreamingOptions,
                Stats,
                SessionManager,
                Cameras,
                dispatcher,
                ControlMap,
                Forwarder,
                ListenerManager);
        }
    }

    [Fact]
    public async Task ForwardRtpAsync_ForwardsPacketToSession()
    {
        var fixture = new TestFixture();
        var cam = new CameraStream { CameraId = "cam-3", VideoPayloadType = 98, Sdp = ["v=0"] };
        fixture.Server.RegisterOrUpdateCamera(cam);

        var received = false;
        var testTransport = new CapturingRtpTransport(onData: _ => received = true, onControl: _ => { });
        var connection = new RtspServerConnection(new SessionManagerTests.FakeRtspTransportAccessor().Transport);
        var session = new ClientSession
        {
            Connection = connection,
            IsPlaying = true,
            Transport = testTransport,
            UseUdp = false
        };

        cam.Sessions.Add(session);

        var rtpPacket = new byte[12];
        rtpPacket[0] = 0x80;
        rtpPacket[1] = 98;

        await fixture.Server.ForwardRtpAsync("cam-3", rtpPacket, TestContext.Current.CancellationToken);

        Assert.True(received);
    }

    [Fact]
    public async Task ForwardRtcpAsync_ForwardsPacketToSession()
    {
        var fixture = new TestFixture();
        var cam = new CameraStream { CameraId = "cam-5", VideoPayloadType = 100, Sdp = ["v=0"] };
        fixture.Server.RegisterOrUpdateCamera(cam);

        var received = false;
        var testTransport = new CapturingRtpTransport(onData: _ => { }, onControl: _ => received = true);
        var connection = new RtspServerConnection(new SessionManagerTests.FakeRtspTransportAccessor().Transport);
        var session = new ClientSession
        {
            Connection = connection,
            IsPlaying = true,
            Transport = testTransport,
            UseUdp = false
        };

        cam.Sessions.Add(session);

        var rtcpPacket = new byte[8];
        rtcpPacket[0] = 0x81;
        rtcpPacket[1] = 201;

        await fixture.Server.ForwardRtcpAsync("cam-5", rtcpPacket, TestContext.Current.CancellationToken);

        Assert.True(received);
    }

    [Fact]
    public void BuildPublicUrl_ReturnsExpectedFormat()
    {
        var fixture = new TestFixture();
        var url = fixture.Server.BuildPublicUrl("localhost", 8554, "cam-6");
        Assert.Equal("rtsp://localhost:8554/cam-6", url);
    }

    private class DummyListenerManager : IListenerManager
    {
        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void RegisterOnAccepted(Func<RtspServerConnection, Task> onAccepted) { }
    }

    private sealed class CapturingRtpTransport(Action<ReadOnlyMemory<byte>> onData, Action<ReadOnlyMemory<byte>> onControl) : IRtpTransport
    {
        public Task SendFrameAsync(int channel, ReadOnlyMemory<byte> frame, CancellationToken cancellationToken = default)
        {
            if (channel % 2 == 0)
            {
                onData(frame);
            }
            else
            {
                onControl(frame);
            }

            return Task.CompletedTask;
        }

        public Task WriteToDataPortAsync(ReadOnlyMemory<byte> packet)
        {
            onData(packet);
            return Task.CompletedTask;
        }

        public Task WriteToControlPortAsync(ReadOnlyMemory<byte> packet)
        {
            onControl(packet);
            return Task.CompletedTask;
        }

        public void Dispose() { }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
