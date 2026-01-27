using System.Collections.Concurrent;
using System.Net.Security;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NestProxy.Server.Core;
using NestProxy.Server.Models;
using NestProxy.Server.Streaming.Downstream.Server;
using NestProxy.Server.Streaming.Downstream.Server.Dispatching;
using NestProxy.Server.Streaming.Upstream;

using Xunit;

namespace NestProxy.Tests;

public class UpstreamByeInjectionTests
{
    private class CapturingLocalServer : ILocalRtspServer
    {
        public ConcurrentQueue<byte[]> RtcpPackets { get; } = new();
        public void RegisterOrUpdateCamera(CameraStream stream) { }
        public void ForwardRtp(string path, ReadOnlyMemory<byte> rtpPacket) { }
        public ValueTask ForwardRtpAsync(string cameraPath, ReadOnlyMemory<byte> rtpPacket, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public void ForwardRtcp(string path, ReadOnlyMemory<byte> rtcpPacket) => RtcpPackets.Enqueue(rtcpPacket.ToArray());
        public ValueTask ForwardRtcpAsync(string cameraPath, ReadOnlyMemory<byte> rtcpPacket, CancellationToken cancellationToken = default) { RtcpPackets.Enqueue(rtcpPacket.ToArray()); return ValueTask.CompletedTask; }
        public string BuildPublicUrl(string host, int port, string path) => $"rtsp://{host}:{port}/{path}";
        public string? DescribeForTest(string cameraPath) => null;
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private class OptionsStub : IOptions<StreamingOptions>
    {
        public StreamingOptions Value { get; } = new() { KeepaliveEnabled = false };
    }

    private class MockSessionFactory : IUpstreamSessionFactory
    {
        private readonly IRtspResponseFormatter _formatter;

        public MockSessionFactory(IRtspResponseFormatter formatter)
        {
            _formatter = formatter;
        }

        public IUpstreamSession Create(string cameraId, RemoteCertificateValidationCallback certValidator, string instanceId)
        {
            IUpstreamSessionStateMachine stateMachine = new UpstreamSessionStateMachine(cameraId, instanceId, NullLogger.Instance);
            IUpstreamSessionStatistics statistics = new UpstreamSessionStatistics(cameraId, instanceId, NullLogger.Instance);
            return new UpstreamSession(cameraId, NullLogger.Instance, certValidator, instanceId, _formatter, stateMachine, statistics);
        }
    }

    [Fact]
    public async Task PromotionWithoutUpstreamBye_InjectsSyntheticBye()
    {
        var local = new CapturingLocalServer();
        var bindingSelector = new NestProxy.Server.Streaming.Forwarding.Binding.BindingSelector();
        var formatter = new RtspResponseFormatter();
        var sessionFactory = new MockSessionFactory(formatter);
        var orchestrator = new UpstreamOrchestrator("cam-42", NullLogger.Instance, local, bindingSelector, new OptionsStub(), sessionFactory);

        // Create real UpstreamSession instances but DO NOT connect.
        var stateMachine1 = new UpstreamSessionStateMachine("cam-42", "1", NullLogger.Instance);
        var statistics1 = new UpstreamSessionStatistics("cam-42", "1", NullLogger.Instance);
        var primary = new UpstreamSession("cam-42", NullLogger.Instance, (_, __, ___, ____) => true, "1", formatter, stateMachine1, statistics1);

        var stateMachine2 = new UpstreamSessionStateMachine("cam-42", "2", NullLogger.Instance);
        var statistics2 = new UpstreamSessionStatistics("cam-42", "2", NullLogger.Instance);
        var staging = new UpstreamSession("cam-42", NullLogger.Instance, (_, __, ___, ____) => true, "2", formatter, stateMachine2, statistics2);

        // Set LastObservedRtpSsrc on primary via reflection (private setter)
        var lastSsrcProp = typeof(UpstreamSession).GetProperty("LastObservedRtpSsrc");
        lastSsrcProp!.SetValue(primary, (uint)0x11223344);

        var primaryField = typeof(UpstreamOrchestrator).GetField("_primarySession", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var activeField = typeof(UpstreamOrchestrator).GetField("_activeSession", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var stagingField = typeof(UpstreamOrchestrator).GetField("_stagingSession", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        primaryField!.SetValue(orchestrator, primary);
        activeField!.SetValue(orchestrator, primary);
        stagingField!.SetValue(orchestrator, staging);

        // Call private PromoteStagingSession(newSession)
        var promoteMethod = typeof(UpstreamOrchestrator).GetMethod("PromoteStagingSession", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        promoteMethod!.Invoke(orchestrator, [staging]);

        await Task.Delay(150, TestContext.Current.CancellationToken); // allow async task to run async teardown & injection
        Assert.Contains(local.RtcpPackets, pkt => pkt.Length >= 2 && pkt[1] == 203);
    }
}