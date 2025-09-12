namespace NestProxy.Rtsp.Messages;

/// <summary>
/// Represents an RTSP response message.
/// </summary>
public class RtspResponse : RtspMessage
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RtspResponse"/> class.
    /// </summary>
    public RtspResponse()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RtspResponse"/> class.
    /// </summary>
    /// <param name="statusCode">The status code.</param>
    /// <param name="reasonPhrase">The reason phrase.</param>
    /// <param name="version">The RTSP version.</param>
    public RtspResponse(RtspStatusCode statusCode, string reasonPhrase, string version)
    {
        StatusCode = statusCode;
        ReasonPhrase = reasonPhrase;
        Version = version;
    }

    /// <summary>
    /// Gets or sets the status code (e.g., 200, 404, 500).
    /// </summary>
    public int ReturnCode
    {
        get => (int)StatusCode;
        set => StatusCode = (RtspStatusCode)value;
    }

    /// <summary>
    /// Gets or sets the status code enum.
    /// </summary>
    public RtspStatusCode StatusCode { get; set; }

    /// <summary>
    /// Gets or sets the reason phrase (e.g., "OK", "Not Found").
    /// </summary>
    public string ReasonPhrase { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the original request that this response corresponds to.
    /// </summary>
    public RtspRequest? OriginalRequest { get; set; }

    /// <summary>
    /// Gets a value indicating whether the response indicates success (2xx status code).
    /// </summary>
    public bool IsSuccessStatusCode => ReturnCode >= 200 && ReturnCode < 300;

    /// <summary>
    /// Converts the response to its string representation for sending over the wire.
    /// </summary>
    public override string ToString()
    {
        var sb = new System.Text.StringBuilder();
        
        // Status line: VERSION STATUS_CODE REASON_PHRASE
        sb.Append(Version).Append(' ').Append((int)StatusCode).Append(' ').Append(ReasonPhrase).Append("\r\n");
        
        // Headers
        foreach (var header in Headers)
        {
            sb.Append(header.Key).Append(": ").Append(header.Value).Append("\r\n");
        }
        
        // Empty line
        sb.Append("\r\n");
        
        // Body
        if (Data.Length > 0)
        {
            sb.Append(GetBodyAsString());
        }
        
        return sb.ToString();
    }
}
