namespace NestProxy.Server.Streaming.Downstream.Server.Describe;

/// <summary>
/// Abstraction for SDP rewriting for downstream clients.
/// </summary>
public interface ISdpRewriter
{
    /// <summary>
    /// Generates a rewritten representation of the provided lines based on the specified media type options.
    /// </summary>
    /// <param name="lines">The collection of input lines to be processed. May be null, in which case no lines will be rewritten.</param>
    /// <param name="isVideo">
    /// Indicates whether the rewrite should be performed with video-specific formatting or rules. Set to <see langword="true"/>
    /// for video content; otherwise, <see langword="false"/>.
    /// </param>
    /// <param name="isAudio">
    /// Indicates whether the rewrite should be performed with audio-specific formatting or rules. Set to <see langword="true"/>
    /// fora udio content; otherwise, <see langword="false"/>.
    /// </param>
    /// <returns>
    /// A string containing the rewritten result based on the input lines and media type options. Returns an empty
    /// string if no lines are provided.
    /// </returns>
    string Rewrite(IEnumerable<string>? lines, bool isVideo, bool isAudio);
}
