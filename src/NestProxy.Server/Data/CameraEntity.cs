using System.Text.Json.Serialization;

namespace NestProxy.Server.Data;

/// <summary>
/// Persistent camera record representing a Google Nest camera or doorbell device with local streaming metadata.
/// </summary>
/// <remarks>
/// Used by the camera repository for storing per-device state. For API contracts, use Camera from NestProxy.Shared.
/// </remarks>
public class CameraEntity
{
    /// <summary>
    /// The unique identifier for the camera entity (local endpoint path).
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Full SDM device resource name (enterprises/..../devices/...).
    /// </summary>
    public string DeviceName { get; set; } = string.Empty;

    /// <summary>
    /// User friendly display name (customName fallback or resource name).
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Optional room name derived from SDM parent relations.
    /// </summary>
    public string? Room { get; set; }

    /// <summary>
    /// Indicates whether the camera is enabled for local proxying.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Last RTSP URL retrieved from Google (for diagnostics).
    /// </summary>
    [JsonIgnore]
    public string? LastRtspUrl { get; set; }

    /// <summary>
    /// Token required to extend the live stream.
    /// </summary>
    [JsonIgnore]
    public string? StreamExtensionToken { get; set; }

    /// <summary>
    /// Expiration timestamp of the current live stream (UTC).
    /// </summary>
    [JsonIgnore]
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Last error message encountered during relay operations (if any).
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>
    /// Timestamp of last update to camera metadata.
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
