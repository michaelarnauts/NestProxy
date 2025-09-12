using NestProxy.Server.Models;

namespace NestProxy.Server.Services;

/// <summary>
/// Service abstraction for managing Google OAuth and Device Access configuration settings.
/// </summary>
/// <remarks>
/// This interface separates configuration management concerns from OAuth authentication operations,
/// following the Single Responsibility Principle.
/// </remarks>
public interface IGoogleSettingsService
{
    /// <summary>
    /// Retrieves the current Google authentication options, with sensitive values masked for display.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A <see cref="GoogleAuthOptions"/> instance with masked client secret suitable for API responses.
    /// </returns>
    Task<GoogleAuthOptions> GetCurrentOptionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the current Google authentication options without masking, for internal use.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A <see cref="GoogleAuthOptions"/> instance with unmasked values for authentication operations.
    /// </returns>
    Task<GoogleAuthOptions> GetOptionsForAuthenticationAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates Google OAuth and Device Access configuration settings.
    /// </summary>
    /// <param name="options">Updated options containing ClientId, ClientSecret, ProjectId, and RedirectUri.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A task representing the asynchronous update operation.
    /// </returns>
    Task UpdateAsync(GoogleAuthOptions options, CancellationToken cancellationToken = default);
}
