using NestProxy.Server.Models;

namespace NestProxy.Server.Data;

/// <summary>
/// Repository abstraction for Google OAuth and Device Access configuration persistence.
/// </summary>
/// <remarks>
/// This interface follows the Repository pattern to provide a clean boundary between
/// the service layer and data access layer for Google settings.
/// </remarks>
public interface IGoogleSettingsRepository
{
    /// <summary>
    /// Retrieves the Google settings entity.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The Google settings entity if found; otherwise, null.
    /// </returns>
    Task<GoogleSettings?> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or updates Google settings in the database.
    /// </summary>
    /// <param name="settings">The settings entity to persist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    Task UpsertAsync(GoogleSettings settings, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists all pending changes to the underlying data store.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The number of state entries written to the database.
    /// </returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
