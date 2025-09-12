using NestProxy.Rtsp.Messages;

namespace NestProxy.Server.Streaming.Downstream.Server.Transport;

/// <summary>
/// Abstraction for RTSP transport negotiation and port allocation for downstream clients.
/// </summary>
public interface ITransportNegotiator
{
    RtspTransport? NegotiateTransport(RtspTransport[] requested);
}
