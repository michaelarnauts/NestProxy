using NestProxy.Rtsp.Messages;
using NestProxy.Rtsp.Server;

namespace NestProxy.Server.Streaming.Downstream.Server.Dispatching;

/// <summary>
/// Defines the contract for an RTSP request handler that processes a specific RTSP method
/// (DESCRIBE, SETUP, PLAY, TEARDOWN, etc.) and sends an appropriate response.
/// </summary>
public interface IRtspRequestHandler
{
    /// <summary>
    /// Handles a single RTSP request and sends the appropriate response.
    /// </summary>
    /// <param name="connection">The downstream RTSP connection.</param>
    /// <param name="request">The parsed RTSP request.</param>
    /// <param name="logPrefix">Structured log prefix (e.g., "[camera-1] [client-abc]").</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    Task HandleAsync(RtspServerConnection connection, RtspRequest request, string logPrefix, CancellationToken cancellationToken = default);
}
