namespace NestProxy.Server.Core;

/// <summary>
/// Provides thread-safe access to the current runtime state of camera relays.
/// </summary>
public interface ICameraRuntimeState
{
    /// <summary>
    /// Updates the running state for a camera. Passing <c>false</c> removes the camera from the running set.
    /// </summary>
    /// <param name="cameraId">Camera identifier (local endpoint path).</param>
    /// <param name="isRunning">True when the camera relay is active; false otherwise.</param>
    void SetRunning(string cameraId, bool isRunning);

    /// <summary>
    /// Indicates whether the specified camera relay is currently running.
    /// </summary>
    /// <param name="cameraId">Camera identifier (local endpoint path).</param>
    /// <returns>True if running; otherwise false.</returns>
    bool IsRunning(string cameraId);

    /// <summary>
    /// Gets a snapshot of camera identifiers currently marked as running.
    /// </summary>
    IReadOnlyCollection<string> RunningCameraIds { get; }

    /// <summary>
    /// Gets the count of running camera relays.
    /// </summary>
    int RunningCount { get; }
}
