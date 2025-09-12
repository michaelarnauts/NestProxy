namespace NestProxy.Rtsp.Protocol;

/// <summary>
/// Interface for parsing RTSP messages from a stream.
/// </summary>
public interface IRtspMessageParser
{
    /// <summary>
    /// Attempts to parse the next message or interleaved frame from the stream.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A parsed message, interleaved frame, or null if more data is needed.</returns>
    Task<object?> ParseAsync(Stream stream, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current parser state.
    /// </summary>
    RtspParserState State { get; }

    /// <summary>
    /// Resets the parser to its initial state, discarding any buffered data.
    /// </summary>
    void Reset();
}
