namespace NestProxy.Rtsp.Messages;

/// <summary>
/// Represents an RTSP PLAY request.
/// </summary>
public sealed class RtspRequestPlay : RtspRequest
{
    /// <summary>
    /// Initializes a new instance of the RtspRequestPlay class, representing an RTSP PLAY request.
    /// </summary>
    /// <remarks>
    /// This constructor sets the Method property to "PLAY" to indicate the request type according to
    /// the RTSP protocol.
    /// </remarks>
    public RtspRequestPlay()
    {
        Method = "PLAY";
    }

    /// <summary>
    /// Gets or sets the Range header value.
    /// </summary>
    public string? Range
    {
        get => Headers.TryGetValue("Range", out var value) ? value : null;
        set
        {
            if (value is not null)
            {
                Headers["Range"] = value;
            }
            else
            {
                Headers.Remove("Range");
            }
        }
    }
}
