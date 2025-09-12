namespace NestProxy.Server.Models;

/// <summary>
/// Result type for <see cref="ExtendRtspStreamAsync"/> containing updated stream extension token and expiration.
/// </summary>
/// <param name="StreamToken">The updated stream token.</param>
/// <param name="StreamExtensionToken">The updated stream extension token.</param>
/// <param name="ExpiresAt">The expiration time of the extended stream.</param>
public record StreamExtensionResult(string StreamToken, string StreamExtensionToken, DateTime ExpiresAt);