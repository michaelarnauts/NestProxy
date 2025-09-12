using System.Text;

namespace NestProxy.Rtsp.Messages;

/// <summary>
/// Base class for RTSP messages (requests and responses).
/// </summary>
public abstract class RtspMessage
{
    private readonly Dictionary<string, string> _headers = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets or sets the RTSP protocol version (e.g., "RTSP/1.0").
    /// </summary>
    public string Version { get; set; } = "RTSP/1.0";

    /// <summary>
    /// Gets the headers dictionary.
    /// </summary>
    public Dictionary<string, string> Headers => _headers;

    /// <summary>
    /// Gets or sets the message body data.
    /// </summary>
    public ReadOnlyMemory<byte> Data { get; set; }

    /// <summary>
    /// Gets or sets the CSeq header value.
    /// </summary>
    public int CSeq
    {
        get => _headers.TryGetValue("CSeq", out var value) && int.TryParse(value, out var cseq) ? cseq : 0;
        set => _headers["CSeq"] = value.ToString();
    }

    /// <summary>
    /// Gets or sets the Session header value.
    /// </summary>
    public string? Session
    {
        get => _headers.TryGetValue("Session", out var value) ? value : null;
        set
        {
            if (value is not null)
            {
                _headers["Session"] = value;
            }
            else
            {
                _headers.Remove("Session");
            }
        }
    }

    /// <summary>
    /// Gets or sets the Content-Type header value.
    /// </summary>
    public string? ContentType
    {
        get => _headers.TryGetValue("Content-Type", out var value) ? value : null;
        set
        {
            if (value is not null)
            {
                _headers["Content-Type"] = value;
            }
            else
            {
                _headers.Remove("Content-Type");
            }
        }
    }

    /// <summary>
    /// Gets the Content-Length header value (computed from Data if not explicitly set).
    /// </summary>
    public int ContentLength
    {
        get
        {
            if (_headers.TryGetValue("Content-Length", out var value) && int.TryParse(value, out var length))
            {
                return length;
            }

            return Data.Length;
        }
        set => _headers["Content-Length"] = value.ToString();
    }

    /// <summary>
    /// Gets the body as a UTF-8 string.
    /// </summary>
    public string? GetBodyAsString()
    {
        if (Data.Length == 0)
        {
            return null;
        }

        return Encoding.UTF8.GetString(Data.Span);
    }

    /// <summary>
    /// Sets the body from a UTF-8 string.
    /// </summary>
    public void SetBodyFromString(string body)
    {
        if (string.IsNullOrEmpty(body))
        {
            Data = ReadOnlyMemory<byte>.Empty;
        }
        else
        {
            Data = Encoding.UTF8.GetBytes(body);
            ContentLength = Data.Length;
        }
    }

    /// <summary>
    /// Gets or sets the message body as a string (convenience property).
    /// </summary>
    public string? Body
    {
        get => GetBodyAsString();
        set
        {
            if (value == null)
            {
                Data = ReadOnlyMemory<byte>.Empty;
            }
            else
            {
                SetBodyFromString(value);
            }
        }
    }
}
