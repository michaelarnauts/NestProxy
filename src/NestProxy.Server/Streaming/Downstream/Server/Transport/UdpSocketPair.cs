using System.Net.Sockets;

namespace NestProxy.Server.Streaming.Downstream.Server.Transport;

/// <summary>
/// Represents a UDP socket pair for RTP/RTCP with their assigned ports.
/// </summary>
public sealed record UdpSocketPair(Socket Rtp, Socket Rtcp, int RtpPort, int RtcpPort);
