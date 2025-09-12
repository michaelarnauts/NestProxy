using NestProxy.Rtsp.Messages;

namespace NestProxy.Rtsp.Protocol;

/// <summary>
/// Interface for serializing RTSP messages to a stream.
/// </summary>
public interface IRtspMessageSerializer
{
    /// <summary>
    /// Serializes an RTSP request to the stream.
    /// </summary>
    /// <param name="request">The request to serialize.</param>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SerializeAsync(RtspRequest request, Stream stream, CancellationToken cancellationToken = default);

    /// <summary>
    /// Serializes an RTSP response to the stream.
    /// </summary>
    /// <param name="response">The response to serialize.</param>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SerializeAsync(RtspResponse response, Stream stream, CancellationToken cancellationToken = default);

    /// <summary>
    /// Serializes an interleaved frame to the stream.
    /// </summary>
    /// <param name="frame">The frame to serialize.</param>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SerializeAsync(InterleavedFrame frame, Stream stream, CancellationToken cancellationToken = default);
}
