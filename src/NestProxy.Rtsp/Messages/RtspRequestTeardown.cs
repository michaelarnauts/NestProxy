namespace NestProxy.Rtsp.Messages;

/// <summary>
/// Represents an RTSP TEARDOWN request.
/// </summary>
public sealed class RtspRequestTeardown : RtspRequest
{
    /// <summary>
    /// Initializes a new instance of the RtspRequestTeardown class, representing an RTSP TEARDOWN request.
    /// </summary>
    /// <remarks>
    /// Use this constructor to create a request that instructs the RTSP server to terminate the
    /// session and release associated resources. The Method property is set to "TEARDOWN" by default.
    /// </remarks>
    public RtspRequestTeardown()
    {
        Method = "TEARDOWN";
    }
}
