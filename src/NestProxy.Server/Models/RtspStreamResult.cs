namespace NestProxy.Server.Models;

/// <summary>
/// Result type for <see cref="GenerateRtspStreamAsync"/> containing the stream URL and tokens.
/// </summary>
/// <param name="RtspUrl">The RTSP URL for the live stream.</param>
/// <param name="StreamExtensionToken">The token used to extend the stream.</param>
/// <param name="StreamToken">The stream token.</param>
/// <param name="ExpiresAt">The expiration time of the stream.</param>
public record RtspStreamResult(string RtspUrl, string StreamExtensionToken, string StreamToken, DateTime ExpiresAt);
