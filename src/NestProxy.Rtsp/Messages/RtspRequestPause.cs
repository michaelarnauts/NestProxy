namespace NestProxy.Rtsp.Messages;

/// <summary>
/// Represents an RTSP PAUSE request.
/// </summary>
public sealed class RtspRequestPause : RtspRequest
{
    /// <summary>
    /// Initializes a new instance of the RtspRequestPause class, representing an RTSP PAUSE request.
    /// </summary>
    /// <remarks>
    /// Use this constructor to create a request that instructs the RTSP server to temporarily halt
    /// media stream delivery without closing the session. The PAUSE method is typically used to pause playback or
    /// recording in an RTSP session.
    /// </remarks>
    public RtspRequestPause()
    {
        Method = "PAUSE";
    }
}
