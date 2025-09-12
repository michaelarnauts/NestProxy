namespace NestProxy.Rtsp.Protocol;

/// <summary>
/// Represents the current state of the RTSP message parser.
/// </summary>
public enum RtspParserState
{
    /// <summary>
    /// Waiting for the start of a new message or interleaved frame.
    /// </summary>
    WaitingForMessage,

    /// <summary>
    /// Reading the request/response line (first line).
    /// </summary>
    ReadingStartLine,

    /// <summary>
    /// Reading RTSP headers.
    /// </summary>
    ReadingHeaders,

    /// <summary>
    /// Reading the message body.
    /// </summary>
    ReadingBody,

    /// <summary>
    /// Reading an interleaved binary frame (RTP/RTCP).
    /// </summary>
    ReadingInterleavedFrame,

    /// <summary>
    /// Message is complete and ready for processing.
    /// </summary>
    MessageComplete,

    /// <summary>
    /// An error occurred during parsing.
    /// </summary>
    Error
}
