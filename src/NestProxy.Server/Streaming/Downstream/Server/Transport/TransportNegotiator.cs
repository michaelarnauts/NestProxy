using NestProxy.Rtsp.Messages;

namespace NestProxy.Server.Streaming.Downstream.Server.Transport;

/// <summary>
/// Handles RTSP transport negotiation and port allocation for downstream clients.
/// </summary>
public sealed class TransportNegotiator(ITransportAllocator allocator) : ITransportNegotiator
{
    /// <summary>
    /// Selects and allocates the appropriate transport for a SETUP request.
    /// </summary>
    /// <param name="requested">Requested transports from the client.</param>
    /// <returns>The selected and allocated RtspTransport, or null if unsupported.</returns>
    public RtspTransport? NegotiateTransport(RtspTransport[] requested)
    {
        RtspTransport? tcpInterleaved = requested.FirstOrDefault(t => t.LowerTransport.Equals("TCP", StringComparison.OrdinalIgnoreCase) && !t.IsMulticast && t.Interleaved != null);
        RtspTransport? udpUnicast = requested.FirstOrDefault(t => t.LowerTransport.Equals("UDP", StringComparison.OrdinalIgnoreCase) && t.ClientPort != null && !t.IsMulticast);

        if (tcpInterleaved != null)
        {
            // No allocation needed for TCP interleaved
            return tcpInterleaved;
        }
        else if (udpUnicast != null)
        {
            // Allocate UDP ports using TryAllocateUdpPair
            var allocation = allocator.TryAllocateUdpPair();
            if (allocation == null)
            {
                return null;
            }

            var rtpPort = allocation.RtpPort;
            var rtcpPort = allocation.RtcpPort;
            return new RtspTransport
            {
                LowerTransport = "UDP",
                ClientPort = udpUnicast.ClientPort,
                ServerPort = new PortCouple(rtpPort, rtcpPort)
            };
        }

        return null;
    }
}
