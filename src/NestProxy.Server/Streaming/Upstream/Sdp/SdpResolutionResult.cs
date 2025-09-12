namespace NestProxy.Server.Streaming.Upstream.Sdp;

/// <summary>
/// Represents the result of resolving an SDP (Session Description Protocol) source, including base URLs, track URIs,
/// SDP lines, and optional payload type information.
/// </summary>
public sealed class SdpResolutionResult
{
    /// <summary>
    /// Base URL resolved from DESCRIBE.
    /// </summary>
    public required string BaseUrl { get; init; }

    /// <summary>
    /// Aggregate (session-level) URL if provided (used for PLAY / session-level SETUP fallback).
    /// </summary>
    public string? AggregateUrl { get; init; }

    /// <summary>
    /// Gets the collection of URIs that identify the tracks associated with this instance.
    /// </summary>
    public required IReadOnlyList<Uri> TrackUris { get; init; }

    /// <summary>
    /// Gets the collection of Session Description Protocol (SDP) lines associated with this instance.
    /// </summary>
    /// <remarks>
    /// Each element in the array represents a single line from the SDP message, in the order they
    /// appear. The array is required and cannot be null.
    /// </remarks>
    public required string[] SdpLines { get; init; }

    /// <summary>
    /// Gets the RTP payload type identifier for the video stream, if specified.
    /// </summary>
    /// <remarks>
    /// The RTP payload type is used to indicate the format of the video data in RTP packets. If this
    /// property is null, the default payload type for the negotiated codec will be used.
    /// </remarks>
    public int? VideoPayloadType { get; init; }

    /// <summary>
    /// Gets the RTP payload type identifier for the audio stream, if specified.
    /// </summary>
    /// <remarks>
    /// The RTP payload type is used to indicate the format of the audio data in RTP packets. If this
    /// property is null, the default payload type for the negotiated codec will be used.
    /// </remarks>
    public int? AudioPayloadType { get; init; }
}
