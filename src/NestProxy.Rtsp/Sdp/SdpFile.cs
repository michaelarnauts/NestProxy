namespace NestProxy.Rtsp.Sdp;

/// <summary>
/// Represents a parsed SDP (Session Description Protocol) file.
/// </summary>
public sealed class SdpFile
{
    /// <summary>
    /// Gets or sets the SDP version (v= line).
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// Gets or sets the origin information (o= line).
    /// </summary>
    public string? Origin { get; set; }

    /// <summary>
    /// Gets or sets the session name (s= line).
    /// </summary>
    public string? SessionName { get; set; }

    /// <summary>
    /// Gets or sets the session description (i= line).
    /// </summary>
    public string? SessionDescription { get; set; }

    /// <summary>
    /// Gets or sets the connection information (c= line at session level).
    /// </summary>
    public string? Connection { get; set; }

    /// <summary>
    /// Gets or sets the timing information (t= line).
    /// </summary>
    public string? Timing { get; set; }

    /// <summary>
    /// Gets or sets session-level attributes.
    /// </summary>
    public List<SdpAttribute> Attributes { get; set; } = [];

    /// <summary>
    /// Gets or sets the media sections.
    /// </summary>
    public List<SdpMedia> Medias { get; set; } = [];

    /// <summary>
    /// Gets the value of a session-level attribute by key.
    /// </summary>
    public string? GetAttributeValue(string key)
    {
        return Attributes.FirstOrDefault(a => a.Key.Equals(key, StringComparison.OrdinalIgnoreCase))?.Value;
    }

    /// <summary>
    /// Gets all session-level attributes with the specified key.
    /// </summary>
    public IEnumerable<SdpAttribute> GetAttributes(string key)
    {
        return Attributes.Where(a => a.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
    }
}
