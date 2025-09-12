namespace NestProxy.Server.Core;

/// <summary>
/// Represents a single upstream Google Nest camera stream registration including its SDP metadata
/// and the active downstream client sessions subscribed to it.
/// </summary>
public class CameraStream
{
    /// <summary>
    /// Unique identifier for the camera (local endpoint path), also used as the RTSP path.
    /// </summary>
    public required string CameraId { get; init; }

    /// <summary>
    /// Primary video RTP payload type advertised upstream (dynamic PT values allowed).
    /// </summary>
    public int VideoPayloadType { get; set; } = 97;

    /// <summary>
    /// Primary audio RTP payload type advertised upstream (if present).
    /// </summary>
    public int AudioPayloadType { get; set; } = 96;

    /// <summary>
    /// Active downstream client sessions subscribed to this camera.
    /// </summary>
    public List<ClientSession> Sessions { get; } = [];

    /// <summary>
    /// Raw SDP lines captured from the upstream DESCRIBE response.
    /// </summary>
    public string[]? Sdp { get; set; }

    /// <summary>
    /// Subset of upstream media (video) lines retained for downstream SDP synthesis.
    /// </summary>
    public string[]? UpstreamVideoMediaLines { get; set; }

    /// <summary>
    /// Subset of upstream media (audio) lines retained for downstream SDP synthesis.
    /// </summary>
    public string[]? UpstreamAudioMediaLines { get; set; }

    /// <summary>
    /// Pre-parsed mapping of SDP control attribute values (e.g. trackID=1, trackID=2, absolute control URIs)
    /// to their RTP payload type numbers for faster lookup during SETUP.
    /// </summary>
    public Dictionary<string, int> ControlToPayloadMap { get; } = new(StringComparer.OrdinalIgnoreCase);
}