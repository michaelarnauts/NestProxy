namespace NestProxy.Server.Streaming.Forwarding;

/// <summary>
/// Forwards incoming RTP/RTCP packets from an upstream camera path to all active client sessions.
/// Encapsulates SSRC-first binding selection and payload-type bootstrap heuristics.
/// </summary>
/// <summary>
/// Forwards incoming RTP/RTCP packets from an upstream camera path to all active client sessions.
/// Encapsulates SSRC-first binding selection and payload-type bootstrap heuristics.
/// </summary>
public interface IStreamForwarder
{
    /// <summary>
    /// Forwards an RTP packet to all active client sessions for the specified camera path.
    /// </summary>
    /// <param name="cameraPath">The camera path.</param>
    /// <param name="rtpPacket">The RTP packet to forward.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    ValueTask ForwardRtpAsync(string cameraPath, ReadOnlyMemory<byte> rtpPacket, CancellationToken cancellationToken = default);

    /// <summary>
    /// Forwards an RTCP packet to all active client sessions for the specified camera path.
    /// </summary>
    /// <param name="cameraPath">The camera path.</param>
    /// <param name="rtcpPacket">The RTCP packet to forward.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    ValueTask ForwardRtcpAsync(string cameraPath, ReadOnlyMemory<byte> rtcpPacket, CancellationToken cancellationToken = default);
}