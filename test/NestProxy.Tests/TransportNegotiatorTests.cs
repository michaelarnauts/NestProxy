using System.Net.Sockets;

using NestProxy.Server.Streaming.Downstream.Server.Transport;
using NestProxy.Rtsp.Messages;

using Xunit;

namespace NestProxy.Tests;

public class TransportNegotiatorTests
{
    private sealed class DummyAllocator : ITransportAllocator
    {
        public UdpSocketPair? TryAllocateUdpPair(int maxAttempts = 20)
        {
            return new UdpSocketPair(
                new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp),
                new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp),
                5000, 5001);
        }
    }

    [Fact]
    public void NegotiateTransport_SelectsTcpInterleaved()
    {
        var allocator = new DummyAllocator();
        var negotiator = new TransportNegotiator(allocator);
        var requested = new[] {
            new RtspTransport { LowerTransport = "TCP", Interleaved = new PortCouple(0, 1) }
        };
        var selected = negotiator.NegotiateTransport(requested);
        Assert.NotNull(selected);
        Assert.Equal("TCP", selected.LowerTransport, ignoreCase: true);
        Assert.Equal(new PortCouple(0, 1), selected.Interleaved);
    }

    [Fact]
    public void NegotiateTransport_SelectsUdpUnicastAndAllocatesPorts()
    {
        var allocator = new DummyAllocator();
        var negotiator = new TransportNegotiator(allocator);
        var requested = new[] {
            new RtspTransport { LowerTransport = "UDP", ClientPort = new PortCouple(6000, 6001) }
        };
        var selected = negotiator.NegotiateTransport(requested);
        Assert.NotNull(selected);
        Assert.Equal("UDP", selected.LowerTransport, ignoreCase: true);
        Assert.Equal(new PortCouple(6000, 6001), selected.ClientPort);
        Assert.Equal(new PortCouple(5000, 5001), selected.ServerPort);
    }

    [Fact]
    public void NegotiateTransport_ReturnsNull_WhenNoSupportedTransport()
    {
        var allocator = new DummyAllocator();
        var negotiator = new TransportNegotiator(allocator);
        var requested = new[] {
            new RtspTransport { LowerTransport = "UDP", Destination = "239.0.0.1" }
        };
        var selected = negotiator.NegotiateTransport(requested);
        Assert.Null(selected);
    }
}
