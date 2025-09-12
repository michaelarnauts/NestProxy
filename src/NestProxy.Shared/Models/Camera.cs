namespace NestProxy.Shared.Models;

/// <summary>
/// Represents a Google Nest camera domain model used for application logic and API contracts.
/// </summary>
/// <remarks>
/// This is the domain model exposed to clients and used throughout the application layer.
/// For persistence, see CameraEntity in the server project.
/// The Id corresponds to the LocalEndpointPath.
/// </remarks>
public class Camera
{
    /// <summary>
    /// Gets or sets the unique identifier for the camera (the local endpoint path).
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the full resource name of the device (e.g., "enterprises/xyz/devices/abc").
    /// </summary>
    public string DeviceName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name for the camera.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the room or location label for the camera, if available.
    /// </summary>
    public string? Room { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the camera is enabled for streaming.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the local RTSP URL for this camera, if available.
    /// </summary>
    public string? LocalRtspUrl { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the camera is currently running (actively streaming).
    /// </summary>
    public bool IsRunning { get; set; }

    /// <summary>
    /// Gets or sets the last error message encountered by the camera, if any.
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>
    /// Gets or sets the UTC expiration time for the current stream, if any.
    /// </summary>
    public DateTime? ExpiresAt { get; set; }
}
