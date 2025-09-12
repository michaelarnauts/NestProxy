namespace NestProxy.Rtsp.Messages;

/// <summary>
/// Represents an RTSP DESCRIBE request.
/// </summary>
public sealed class RtspRequestDescribe : RtspRequest
{
    /// <summary>
    /// Initializes a new instance of the RtspRequestDescribe class, representing an RTSP DESCRIBE request.
    /// </summary>
    /// <remarks>
    /// This constructor sets the RTSP method to "DESCRIBE" and configures the request to accept
    /// responses in the Session Description Protocol (SDP) format by setting the "Accept" header to
    /// "application/sdp".
    /// </remarks>
    public RtspRequestDescribe()
    {
        Method = "DESCRIBE";
        Headers["Accept"] = "application/sdp";
    }
}
