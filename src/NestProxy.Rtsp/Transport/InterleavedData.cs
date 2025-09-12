namespace NestProxy.Rtsp.Transport;

/// <summary>
/// Represents interleaved binary data received over an RTSP connection.
/// </summary>
public sealed class InterleavedData
{
    // The interleaved channel the packet was received on.
    public byte Channel { get; init; }

    /// <summary>
    /// The binary data payload.
    /// </summary>
    public byte[] Data { get; init; } = [];
}
