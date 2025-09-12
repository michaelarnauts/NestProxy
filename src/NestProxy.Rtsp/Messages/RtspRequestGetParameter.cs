namespace NestProxy.Rtsp.Messages;

/// <summary>
/// Represents an RTSP GET_PARAMETER request (used for keepalive).
/// </summary>
public sealed class RtspRequestGetParameter : RtspRequest
{
    /// <summary>
    /// Initializes a new instance of the RtspRequestGetParameter class with the RTSP method set to GET_PARAMETER.
    /// </summary>
    public RtspRequestGetParameter()
    {
        Method = "GET_PARAMETER";
    }
}
