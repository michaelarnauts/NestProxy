namespace NestProxy.Rtsp.Sdp;

/// <summary>
/// Represents an SDP media section (m= line and associated attributes).
/// </summary>
public sealed class SdpMedia
{
    /// <summary>
    /// Gets or sets the media type (audio, video, etc.).
    /// </summary>
    public MediaType MediaType { get; set; }

    /// <summary>
    /// Gets or sets the port number.
    /// </summary>
    public int Port { get; set; }

    /// <summary>
    /// Gets or sets the protocol (e.g., "RTP/AVP").
    /// </summary>
    public string Protocol { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the format list (payload types).
    /// </summary>
    public List<string> Formats { get; set; } = [];

    /// <summary>
    /// Gets or sets the connection information for this media (c= line).
    /// </summary>
    public string? Connection { get; set; }

    /// <summary>
    /// Gets or sets the attributes for this media.
    /// </summary>
    public List<SdpAttribute> Attributes { get; set; } = [];

    /// <summary>
    /// Gets the value of a specific attribute by key.
    /// </summary>
    public string? GetAttributeValue(string key)
    {
        return Attributes.FirstOrDefault(a => a.Key.Equals(key, StringComparison.OrdinalIgnoreCase))?.Value;
    }

    /// <summary>
    /// Gets all attributes with the specified key.
    /// </summary>
    public IEnumerable<SdpAttribute> GetAttributes(string key)
    {
        return Attributes.Where(a => a.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
    }
}
