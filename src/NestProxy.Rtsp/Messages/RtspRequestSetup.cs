namespace NestProxy.Rtsp.Messages;

/// <summary>
/// Represents an RTSP SETUP request.
/// </summary>
public sealed class RtspRequestSetup : RtspRequest
{
    /// <summary>
    /// Initializes a new instance of the RtspRequestSetup class with the RTSP SETUP method.
    /// </summary>
    /// <remarks>
    /// Use this constructor to create a request that initiates a media stream setup using the RTSP
    /// protocol. The Method property is set to "SETUP" by default.
    /// </remarks>
    public RtspRequestSetup()
    {
        Method = "SETUP";
    }

    /// <summary>
    /// Gets or sets the Transport header value.
    /// </summary>
    public string? Transport
    {
        get => Headers.TryGetValue("Transport", out var value) ? value : null;
        set
        {
            if (value is not null)
            {
                Headers["Transport"] = value;
            }
            else
            {
                Headers.Remove("Transport");
            }
        }
    }
}
