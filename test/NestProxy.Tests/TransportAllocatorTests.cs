using System.Net.Sockets;
using Xunit;
using NestProxy.Server.Streaming.Downstream.Server.Transport;

namespace NestProxy.Tests;

public class TransportAllocatorTests
{
    [Fact]
    public void TryAllocateUdpPair_ReturnsValidPair()
    {
        var allocator = new TransportAllocator(new Microsoft.Extensions.Logging.Abstractions.NullLogger<TransportAllocator>());
        var result = allocator.TryAllocateUdpPair();
        Assert.NotNull(result);
        Assert.NotNull(result!.Rtp);
        Assert.NotNull(result.Rtcp);
        Assert.True(result.RtpPort % 2 == 0);
        Assert.Equal(result.RtpPort + 1, result.RtcpPort);
        result.Rtp.Dispose();
        result.Rtcp.Dispose();
    }

    [Fact]
    public void TryAllocateUdpPair_ReturnsNull_WhenNoPortsAvailable()
    {
        // Simulate by passing maxAttempts = 0
        var allocator = new TransportAllocator(new Microsoft.Extensions.Logging.Abstractions.NullLogger<TransportAllocator>());
        var result = allocator.TryAllocateUdpPair(0);
        Assert.Null(result);
    }
}
