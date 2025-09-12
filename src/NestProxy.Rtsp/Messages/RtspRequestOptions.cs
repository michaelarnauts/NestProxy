namespace NestProxy.Rtsp.Messages;

/// <summary>
/// Represents an RTSP OPTIONS request.
/// </summary>
public sealed class RtspRequestOptions : RtspRequest
{
    /// <summary>
    /// Initializes a new instance of the RtspRequestOptions class with the RTSP method set to "OPTIONS".
    /// </summary>
    /// <remarks>
    /// Use this constructor to create an RTSP OPTIONS request, which can be sent to a server to
    /// query the supported methods or capabilities.
    /// </remarks>
    public RtspRequestOptions()
    {
        Method = "OPTIONS";
    }
}
