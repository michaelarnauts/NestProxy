namespace NestProxy.Rtsp.Sdp;

/// <summary>
/// Represents an SDP attribute (a= line).
/// </summary>
public sealed class SdpAttribute
{
    /// <summary>
    /// Gets or sets the attribute key.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// Gets or sets the attribute value (may be null for flag attributes).
    /// </summary>
    public string? Value { get; init; }

    /// <summary>
    /// Returns a string representation of the attribute.
    /// </summary>
    public override string ToString()
    {
        return Value is null ? Key : $"{Key}:{Value}";
    }
}
