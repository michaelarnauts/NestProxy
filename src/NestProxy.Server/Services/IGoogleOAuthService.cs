using NestProxy.Server.Models;

namespace NestProxy.Server.Services;

/// <summary>
/// Service abstraction for Google OAuth2 authentication operations including authorization URL
/// generation, code exchange, and token refresh.
/// </summary>
/// <remarks>
/// This interface focuses solely on OAuth authentication flows, separated from configuration
/// management concerns per the Single Responsibility Principle.
/// </remarks>
public interface IGoogleOAuthService
{
    /// <summary>
    /// Builds the Google OAuth2 authorization URL for initiating the consent flow.
    /// </summary>
    /// <param name="state">Opaque state parameter that will be echoed back to the redirect URI for CSRF protection.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A fully qualified authorization endpoint URL the user should be redirected to.
    /// </returns>
    Task<string> BuildAuthorizationUrlAsync(string state, CancellationToken cancellationToken = default);

    /// <summary>
    /// Exchanges an authorization code for access and refresh tokens.
    /// </summary>
    /// <param name="code">Authorization code returned by Google after user consent.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The persisted <see cref="Credentials"/> record containing the new tokens.
    /// </returns>
    /// <exception cref="InvalidOperationException">Thrown when token response parsing fails.</exception>
    /// <exception cref="HttpRequestException">Thrown when the token exchange request fails.</exception>
    Task<Credentials> ExchangeCodeForTokensAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a valid access token, automatically refreshing if expired or about to expire.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A valid access token string, or null if no credentials exist or refresh fails.
    /// </returns>
    Task<string?> GetValidAccessTokenAsync(CancellationToken cancellationToken = default);
}
