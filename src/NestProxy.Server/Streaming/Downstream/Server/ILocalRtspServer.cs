namespace NestProxy.Server.Streaming.Downstream.Server;

/// <summary>
/// Defines the contract for the local RTSP server that relays upstream RTP/RTCP streams to downstream clients.
/// </summary>
public interface ILocalRtspServer
{
    /// <summary>
    /// Registers a new camera or updates an existing registration.
    /// </summary>
    void RegisterOrUpdateCamera(CameraStream stream);

    /// <summary>
    /// Async variant for forwarding RTP packets.
    /// </summary>
    ValueTask ForwardRtpAsync(string cameraPath, ReadOnlyMemory<byte> rtpPacket, CancellationToken cancellationToken = default);

    /// <summary>
    /// Async variant for forwarding RTCP packets.
    /// </summary>
    ValueTask ForwardRtcpAsync(string cameraPath, ReadOnlyMemory<byte> rtcpPacket, CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the public RTSP URL for a camera path.
    /// </summary>
    string BuildPublicUrl(string host, int port, string cameraPath);

    /// <summary>
    /// Stops the RTSP server, closing the listen socket and disposing all active listeners and their sessions.
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken);
}