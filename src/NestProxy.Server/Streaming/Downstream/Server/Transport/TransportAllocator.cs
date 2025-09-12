using System.Net;
using System.Net.Sockets;

namespace NestProxy.Server.Streaming.Downstream.Server.Transport;

/// <summary>
/// Allocates UDP port pairs (RTP/RTCP consecutive even/odd) for downstream transports. This keeps allocation
/// logic centralized so future strategies (reserved ranges, reuse, metrics) can be implemented without touching
/// the RTSP handling logic.
/// </summary>
/// <remarks>Creates a new <see cref="TransportAllocator"/>.</remarks>
public sealed class TransportAllocator(ILogger<TransportAllocator> logger) : ITransportAllocator
{
    private readonly ILogger<TransportAllocator> _logger = logger;

    /// <summary>
    /// Attempts to allocate a UDP socket pair for RTP/RTCP with an even/odd consecutive port pair. Each attempt picks a
    /// random even base port. On bind failure it retries until <paramref name="maxAttempts"/> is reached.
    /// </summary>
    /// <param name="maxAttempts">Maximum number of random base port attempts (default 20).</param>
    /// <returns>
    /// <see cref="UdpSocketPair"/> containing RTP socket, RTCP socket, RTP port and RTCP port; or <c>null</c> if no pair could be allocated.
    /// </returns>
    public UdpSocketPair? TryAllocateUdpPair(int maxAttempts = 20)
    {
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            int basePort = Random.Shared.Next(10000, 60000);
            if ((basePort & 1) == 1)
            {
                basePort++; // enforce even RTP port
            }

            Socket rtp = new(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            Socket rtcp = new(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            try
            {
                rtp.Bind(new IPEndPoint(IPAddress.Any, basePort));
                rtcp.Bind(new IPEndPoint(IPAddress.Any, basePort + 1));
                return new UdpSocketPair(rtp, rtcp, basePort, basePort + 1);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "UDP pair bind failed on attempt {Attempt} for base {Port}", attempt + 1, basePort);

                Safe.Invoke(() => rtp.Dispose());
                Safe.Invoke(() => rtcp.Dispose());
            }
        }

        return null;
    }
}