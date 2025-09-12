using System.Threading;
using System.Threading.Tasks;
using NestProxy.Server.Streaming.Downstream.Server;
using NestProxy.Rtsp.Server;
using NestProxy.Rtsp.Messages;

namespace NestProxy.Server.Streaming.Downstream.Server.Transport;

/// <summary>
/// Abstraction for handling downstream RTSP SETUP requests: selects a transport, applies/creates track bindings
/// and populates the response headers. Keeps transport negotiation logic out of <see cref="LocalRtspServer"/>.
/// </summary>
public interface ITransportSetupService
{
    /// <summary>
    /// Handles a SETUP request for a given listener and client session.
    /// </summary>
    /// <param name="connection">RTSP server connection (downstream client connection).</param>
    /// <param name="request">Incoming SETUP request.</param>
    /// <param name="session">Associated client session.</param>
    /// <param name="camera">Camera stream metadata (for session-level SETUP to create bindings for all tracks).</param>
    /// <param name="payloadTypeForThisSetup">If known, payload type the track corresponds to (may be null for session-level SETUP).</param>
    /// <param name="isSessionLevelSetup">True if this is a session-level SETUP (no specific track control specified).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    Task HandleSetupAsync(RtspServerConnection connection, RtspRequest request, ClientSession session, CameraStream camera, int? payloadTypeForThisSetup, bool isSessionLevelSetup, CancellationToken cancellationToken = default);
}