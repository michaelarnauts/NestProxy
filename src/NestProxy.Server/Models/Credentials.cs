namespace NestProxy.Server.Models;

/// <summary>
/// Stores provider credentials (access/refresh tokens) required for API access.
/// </summary>
public class Credentials
{
    /// <summary>
    /// Identity of the provider (fixed to GoogleNest).
    /// </summary>
    public string Provider { get; set; } = "GoogleNest";

    /// <summary>
    /// Project identifier associated with the credentials.
    /// </summary>
    public string ProjectId { get; set; } = string.Empty;

    /// <summary>
    /// OAuth2 refresh token (long-lived).
    /// </summary>
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>
    /// OAuth2 access token (short-lived).
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// Expiration time of the access token.
    /// </summary>
    public DateTime? ExpiresAt { get; set; }
}