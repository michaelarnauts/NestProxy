namespace NestProxy.Server.Core;

/// <summary>
/// Defines methods for reporting and retrieving streaming statistics for cameras and sessions.
/// </summary>
/// <remarks>
/// Implementations should ensure thread safety and efficient state management.
/// </remarks>
public interface IStatisticsReporter
{
    /// <summary>
    /// Increments the statistics counters for a given camera and session.
    /// </summary>
    /// <param name="cameraPath">The path identifying the camera.</param>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="payloadType">The RTP payload type.</param>
    /// <param name="isRtp">Indicates whether the packet is RTP (true) or RTCP (false).</param>
    void Increment(string cameraPath, string sessionId, int payloadType, bool isRtp);

    /// <summary>
    /// Flushes all accumulated statistics, persisting or resetting as appropriate.
    /// </summary>
    void FlushAll();

    /// <summary>
    /// Attempts to retrieve a snapshot of statistics for a specific camera and session.
    /// </summary>
    /// <param name="cameraPath">The path identifying the camera.</param>
    /// <param name="sessionId">The session identifier.</param>
    /// <returns>
    /// An object containing the statistics snapshot, or <c>null</c> if not available.
    /// </returns>
    object TrySnapshot(string cameraPath, string sessionId);

    /// <summary>
    /// Retrieves a snapshot of all current statistics for all cameras and sessions.
    /// </summary>
    /// <returns>
    /// A dictionary mapping camera/session identifiers to their statistics objects.
    /// </returns>
    IDictionary<string, object> SnapshotAll();
}