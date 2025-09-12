using NestProxy.Rtsp.Server;

namespace NestProxy.Server.Streaming.Downstream.Sessions;

/// <summary>
/// Abstraction over session lifecycle & enumeration to decouple forwarding and server orchestration
/// from the underlying storage and locking strategy.
/// </summary>
public interface ISessionRegistry
{
    /// <summary>
    /// Gets or creates a client session for the specified camera and connection.
    /// </summary>
    /// <param name="camera">The camera stream.</param>
    /// <param name="connection">The RTSP server connection.</param>
    /// <returns>The existing or newly created client session.</returns>
    ClientSession GetOrCreateSession(CameraStream camera, RtspServerConnection connection);

    /// <summary>
    /// Refreshes activity for the specified connection, updating session liveness.
    /// </summary>
    /// <param name="connection">The RTSP server connection.</param>
    void RefreshActivityForConnection(RtspServerConnection connection);

    /// <summary>
    /// Sweeps and removes expired sessions.
    /// </summary>
    /// <returns>The number of sessions removed.</returns>
    int SweepExpiredSessions();

    /// <summary>
    /// Sweeps and removes expired sessions asynchronously.
    /// </summary>
    /// <returns>The number of sessions removed.</returns>
    Task<int> SweepExpiredSessionsAsync();

    /// <summary>
    /// Removes all sessions associated with the specified connection.
    /// </summary>
    /// <param name="connection">The RTSP server connection.</param>
    void RemoveSessionsForConnection(RtspServerConnection connection);

    /// <summary>
    /// Removes all sessions associated with the specified connection asynchronously.
    /// </summary>
    /// <param name="connection">The RTSP server connection.</param>
    Task RemoveSessionsForConnectionAsync(RtspServerConnection connection);

    /// <summary>
    /// Removes the specified client session.
    /// </summary>
    /// <param name="session">The client session to remove.</param>
    void RemoveSession(ClientSession session);

    /// <summary>
    /// Finds and removes the session with the specified session ID, disposing all transports.
    /// </summary>
    /// <param name="sessionId">The session ID to search for.</param>
    /// <returns>
    /// True if a session was found and removed; otherwise false.
    /// </returns>
    bool RemoveSessionById(string sessionId);

    /// <summary>
    /// Returns a thread-safe snapshot of active sessions for a given camera path (may be empty).
    /// </summary>
    /// <param name="cameraPath">The camera path.</param>
    /// <returns>A read-only list of active client sessions.</returns>
    IReadOnlyList<ClientSession> GetSessionsSnapshot(string cameraPath);
}