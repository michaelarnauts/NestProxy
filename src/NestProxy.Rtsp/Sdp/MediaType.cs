namespace NestProxy.Rtsp.Sdp;

/// <summary>
/// Represents SDP media types.
/// </summary>
public enum MediaType
{
    /// <summary>
    /// Unknown or unsupported media type.
    /// </summary>
    Unknown,

    /// <summary>
    /// Audio media.
    /// </summary>
    Audio,

    /// <summary>
    /// Video media.
    /// </summary>
    Video,

    /// <summary>
    /// Application data.
    /// </summary>
    Application,

    /// <summary>
    /// Text media.
    /// </summary>
    Text
}
