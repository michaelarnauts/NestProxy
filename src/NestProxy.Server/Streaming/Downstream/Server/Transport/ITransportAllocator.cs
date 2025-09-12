using System.Net.Sockets;

namespace NestProxy.Server.Streaming.Downstream.Server.Transport;

/// <summary>
/// Abstraction for UDP port pair allocation for downstream RTP/RTCP transport.
/// </summary>
public interface ITransportAllocator
{
    /// <summary>
    /// Attempts to allocate a UDP socket pair for RTP/RTCP with an even/odd consecutive port pair.
    /// </summary>
    /// <param name="maxAttempts">Maximum number of random base port attempts (default 20).</param>
    /// <returns>
    /// <see cref="UdpSocketPair"/> containing RTP socket, RTCP socket, RTP port and RTCP port; or <c>null</c> if no pair could be allocated.
    /// </returns>
    UdpSocketPair? TryAllocateUdpPair(int maxAttempts = 20);
}
