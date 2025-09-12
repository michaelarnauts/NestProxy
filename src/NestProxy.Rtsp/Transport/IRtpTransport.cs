namespace NestProxy.Rtsp.Transport;

/// <summary>
/// Abstraction for sending RTP/RTCP frames over a transport (TCP interleaved or UDP).
/// </summary>
public interface IRtpTransport : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Sends an RTP or RTCP frame over the transport.
    /// </summary>
    /// <param name="channel">Channel number (for TCP interleaved mode).</param>
    /// <param name="frame">Frame data to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SendFrameAsync(int channel, ReadOnlyMemory<byte> frame, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends an RTP packet to the data channel/port.
    /// </summary>
    /// <param name="packet">RTP packet data to send.</param>
    Task WriteToDataPortAsync(ReadOnlyMemory<byte> packet);

    /// <summary>
    /// Sends an RTCP packet to the control channel/port.
    /// </summary>
    /// <param name="packet">RTCP packet data to send.</param>
    Task WriteToControlPortAsync(ReadOnlyMemory<byte> packet);
}
