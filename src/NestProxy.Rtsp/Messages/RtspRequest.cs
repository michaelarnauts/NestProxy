namespace NestProxy.Rtsp.Messages;

/// <summary>
/// Represents an RTSP request message.
/// </summary>
public class RtspRequest : RtspMessage
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RtspRequest"/> class.
    /// </summary>
    public RtspRequest()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RtspRequest"/> class.
    /// </summary>
    /// <param name="method">The RTSP method.</param>
    /// <param name="uri">The request URI.</param>
    /// <param name="version">The RTSP version.</param>
    public RtspRequest(string method, Uri uri, string version)
    {
        Method = method;
        RtspUri = uri;
        Version = version;
    }

    /// <summary>
    /// Gets or sets the request method (e.g., OPTIONS, DESCRIBE, SETUP, PLAY).
    /// </summary>
    public string Method { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the request URI.
    /// </summary>
    public Uri RtspUri { get; set; } = null!;

    /// <summary>
    /// Gets or sets the request URI (alias for compatibility).
    /// </summary>
    public Uri Uri
    {
        get => RtspUri;
        set => RtspUri = value;
    }

    /// <summary>
    /// Creates a response for this request.
    /// </summary>
    /// <param name="statusCode">The status code (e.g., 200).</param>
    /// <param name="reasonPhrase">The reason phrase (e.g., "OK").</param>
    /// <returns>A new RtspResponse.</returns>
    public RtspResponse CreateResponse(int statusCode = 200, string reasonPhrase = "OK")
    {
        return new RtspResponse
        {
            OriginalRequest = this,
            ReturnCode = statusCode,
            ReasonPhrase = reasonPhrase,
            CSeq = CSeq,
            Version = Version
        };
    }

    /// <summary>
    /// Creates a copy of this request (for retries, modifications, etc.).
    /// </summary>
    public RtspRequest Clone()
    {
        var clone = new RtspRequest
        {
            Method = Method,
            RtspUri = RtspUri,
            Version = Version,
            Data = Data
        };

        foreach (var header in Headers)
        {
            clone.Headers[header.Key] = header.Value;
        }

        return clone;
    }

    /// <summary>
    /// Creates an OPTIONS request.
    /// </summary>
    public static RtspRequest CreateOptions(Uri uri)
    {
        var request = new RtspRequest
        {
            Method = "OPTIONS",
            RtspUri = uri,
            Version = "RTSP/1.0"
        };
        
        // Add User-Agent header (commonly expected by RTSP servers)
        request.Headers["User-Agent"] = "NestProxy/1.0";
        
        return request;
    }

    /// <summary>
    /// Creates a DESCRIBE request.
    /// </summary>
    public static RtspRequest CreateDescribe(Uri uri)
    {
        return new RtspRequest
        {
            Method = "DESCRIBE",
            RtspUri = uri,
            Version = "RTSP/1.0"
        };
    }

    /// <summary>
    /// Creates a SETUP request.
    /// </summary>
    public static RtspRequest CreateSetup(Uri uri, string transport)
    {
        var request = new RtspRequest
        {
            Method = "SETUP",
            RtspUri = uri,
            Version = "RTSP/1.0"
        };
        request.Headers["Transport"] = transport;
        return request;
    }

    /// <summary>
    /// Creates a PLAY request.
    /// </summary>
    public static RtspRequest CreatePlay(Uri uri, string? session = null)
    {
        var request = new RtspRequest
        {
            Method = "PLAY",
            RtspUri = uri,
            Version = "RTSP/1.0"
        };
        if (!string.IsNullOrEmpty(session))
        {
            request.Headers["Session"] = session;
        }
        return request;
    }

    /// <summary>
    /// Creates a TEARDOWN request.
    /// </summary>
    public static RtspRequest CreateTeardown(Uri uri, string? session = null)
    {
        var request = new RtspRequest
        {
            Method = "TEARDOWN",
            RtspUri = uri,
            Version = "RTSP/1.0"
        };
        if (!string.IsNullOrEmpty(session))
        {
            request.Headers["Session"] = session;
        }
        return request;
    }

    /// <summary>
    /// Creates a GET_PARAMETER request (used for keep-alive).
    /// </summary>
    public static RtspRequest CreateGetParameter(Uri uri, string? session = null)
    {
        var request = new RtspRequest
        {
            Method = "GET_PARAMETER",
            RtspUri = uri,
            Version = "RTSP/1.0"
        };
        if (!string.IsNullOrEmpty(session))
        {
            request.Headers["Session"] = session;
        }
        return request;
    }

    /// <summary>
    /// Converts the request to its string representation for sending over the wire.
    /// </summary>
    public override string ToString()
    {
        var sb = new System.Text.StringBuilder();
        
        // Request line: METHOD URI VERSION
        sb.Append(Method).Append(' ').Append(RtspUri).Append(' ').Append(Version).Append("\r\n");
        
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
