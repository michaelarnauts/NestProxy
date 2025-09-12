using Microsoft.Extensions.Options;

using NestProxy.Rtsp.Server;

namespace NestProxy.Server.Streaming.Downstream.Sessions;

/// <summary>
/// Manages the lifecycle of downstream <see cref="ClientSession"/> objects for all registered
/// <see cref="CameraStream"/> instances: creation, activity refresh, expiration sweeping and removal.
/// </summary>
/// <remarks>
/// Creates a new <see cref="SessionRegistry"/>.
/// </remarks>
/// <param name="cameraRegistry">Shared camera registry keyed by camera path.</param>
/// <param name="logger">Logger instance.</param>

/// <summary>
/// Manages the lifecycle and tracking of client streaming sessions across all registered cameras.
/// </summary>
/// <remarks>
/// SessionRegistry provides thread-safe operations for creating, retrieving, refreshing, and removing client sessions.
/// It ensures that sessions are expired and cleaned up according to the configured timeout, and that all associated
/// resources are properly disposed.
/// </remarks>
/// <param name="cameraRegistry">The registry containing all available camera streams to associate with client sessions.</param>
/// <param name="options">The streaming options used to configure session timeouts and related behavior.</param>
/// <param name="logger">The logger used to record session management events and diagnostics.</param>
public sealed class SessionRegistry(
    CameraRegistry cameraRegistry,
    IOptions<StreamingOptions> options,
    ILogger<SessionRegistry> logger) : ISessionRegistry
{
    /// <summary>
    /// Gets the existing session bound to the specified connection or creates a new one.
    /// </summary>
    public ClientSession GetOrCreateSession(CameraStream camera, RtspServerConnection connection)
    {
        lock (camera.Sessions)
        {
            ClientSession? session = camera.Sessions.FirstOrDefault(s => s.Connection == connection);
            if (session == null)
            {
                session = new ClientSession { Connection = connection };
                camera.Sessions.Add(session);
            }
            return session;
        }
    }

    /// <summary>
    /// Updates the activity timestamp for all sessions associated with the provided connection.
    /// </summary>
    public void RefreshActivityForConnection(RtspServerConnection connection)
    {
        foreach (CameraStream camera in cameraRegistry)
        {
            lock (camera.Sessions)
            {
                foreach (ClientSession session in camera.Sessions.Where(s => s.Connection == connection))
                {
                    session.LastActivityUtc = DateTime.UtcNow;
                }
            }
        }
    }

    /// <summary>
    /// Expires any sessions that have been idle longer than the configured timeout.
    /// </summary>
    /// <returns>The number of sessions removed.</returns>
    public int SweepExpiredSessions()
    {
        int removed = 0;
        DateTime now = DateTime.UtcNow;
        foreach (CameraStream camera in cameraRegistry)
        {
            List<ClientSession> toRemove = [];
            lock (camera.Sessions)
            {
                foreach (ClientSession session in camera.Sessions)
                {
                    if (now - session.LastActivityUtc > TimeSpan.FromSeconds(options.Value.SessionTimeoutSeconds))
                    {
                        toRemove.Add(session);
                    }
                }

                foreach (ClientSession expired in toRemove)
                {
                    logger.LogDebug("[{CameraId}] [client-{Client}] Session expired", camera.CameraId, expired.SessionId[..Math.Min(8, expired.SessionId.Length)]);
                    camera.Sessions.Remove(expired);

                    // Sync disposal
                    Safe.Invoke(expired.Dispose);

                    removed++;
                }
            }
        }

        return removed;
    }

    /// <summary>
    /// Expires any sessions that have been idle longer than the configured timeout asynchronously.
    /// </summary>
    /// <returns>The number of sessions removed.</returns>
    public async Task<int> SweepExpiredSessionsAsync()
    {
        int removed = 0;
        DateTime now = DateTime.UtcNow;
        foreach (CameraStream camera in cameraRegistry)
        {
            List<ClientSession> toRemove = [];
            lock (camera.Sessions)
            {
                foreach (ClientSession session in camera.Sessions)
                {
                    if (now - session.LastActivityUtc > TimeSpan.FromSeconds(options.Value.SessionTimeoutSeconds))
                    {
                        toRemove.Add(session);
                    }
                }

                foreach (ClientSession expired in toRemove)
                {
                    // Remove from list under lock
                    camera.Sessions.Remove(expired);
                }
            }

            // Dispose outside lock to avoid holding lock during async operations
            foreach (ClientSession expired in toRemove)
            {
                logger.LogDebug("[{CameraId}] [client-{Client}] Session expired", camera.CameraId, expired.SessionId[..Math.Min(8, expired.SessionId.Length)]);
                await Safe.InvokeAsync(async () => await expired.DisposeAsync());
                removed++;
            }
        }

        return removed;
    }

    /// <summary>
    /// Removes (and disposes transports for) all sessions associated with a given RTSP connection.
    /// </summary>
    public void RemoveSessionsForConnection(RtspServerConnection connection)
    {
        foreach (CameraStream camera in cameraRegistry)
        {
            List<ClientSession> removedSessions = [];
            lock (camera.Sessions)
            {
                foreach (ClientSession session in camera.Sessions.Where(s => s.Connection == connection).ToList())
                {
                    camera.Sessions.Remove(session);
                    removedSessions.Add(session);
                }
            }

            foreach (var session in removedSessions)
            {
                logger.LogDebug("[{CameraId}] [client-{Client}] Removing session for connection",
                    camera.CameraId, session.SessionId[..Math.Min(8, session.SessionId.Length)]);
                Safe.Invoke(session.Dispose);
            }
        }
    }

    /// <summary>
    /// Removes (and disposes transports for) all sessions associated with a given RTSP connection asynchronously.
    /// </summary>
    public async Task RemoveSessionsForConnectionAsync(RtspServerConnection connection)
    {
        foreach (CameraStream camera in cameraRegistry)
        {
            List<ClientSession> removedSessions = [];
            lock (camera.Sessions)
            {
                foreach (ClientSession session in camera.Sessions.Where(s => s.Connection == connection).ToList())
                {
                    camera.Sessions.Remove(session);
                    removedSessions.Add(session);
                }
            }

            foreach (var session in removedSessions)
            {
                logger.LogDebug("[{CameraId}] [client-{Client}] Removing session for connection",
                    camera.CameraId, session.SessionId[..Math.Min(8, session.SessionId.Length)]);
                await Safe.InvokeAsync(async () => await session.DisposeAsync());
            }
        }
    }

    /// <summary>
    /// Removes a single session from whichever camera it belongs to, disposing transports.
    /// </summary>
    public void RemoveSession(ClientSession session)
    {
        foreach (CameraStream camera in cameraRegistry)
        {
            lock (camera.Sessions)
            {
                if (camera.Sessions.Remove(session))
                {
                    logger.LogDebug("[{CameraId}] [client-{Client}] Removed session",
                        camera.CameraId, session.SessionId[..Math.Min(8, session.SessionId.Length)]);
                    Safe.Invoke(session.Dispose);
                    break;
                }
            }
        }
        // Fallback disposal if not found in list? No, session might be orphaned.
        // We assume caller handles logic, but cleaning up safely is good.
    }

    /// <inheritdoc />
    public bool RemoveSessionById(string sessionId)
    {
        foreach (CameraStream camera in cameraRegistry)
        {
            ClientSession? target = null;
            lock (camera.Sessions)
            {
                target = camera.Sessions.FirstOrDefault(s => s.SessionId.Equals(sessionId, StringComparison.OrdinalIgnoreCase));
                if (target != null)
                {
                    camera.Sessions.Remove(target);
                }
            }

            if (target != null)
            {
                logger.LogDebug("[{CameraId}] [client-{Client}] Session removed via TEARDOWN", camera.CameraId, sessionId[..Math.Min(8, sessionId.Length)]);
                Safe.Invoke(target.Dispose);
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc />
    public IReadOnlyList<ClientSession> GetSessionsSnapshot(string cameraPath)
    {
        if (!cameraRegistry.TryGetValue(cameraPath, out CameraStream? cam))
        {
            return [];
        }

        lock (cam.Sessions)
        {
            return [.. cam.Sessions];   // Return a copy.
        }
    }
}