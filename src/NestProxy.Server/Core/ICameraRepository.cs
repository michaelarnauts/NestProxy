using NestProxy.Server.Data;

namespace NestProxy.Server.Core;

/// <summary>
/// Repository abstraction for camera persistence operations, decoupling domain logic from storage concerns.
/// </summary>
/// <remarks>
/// This interface provides a clean boundary between the application core and data access layer, improving
/// testability and maintaining dependency inversion.
/// </remarks>
public interface ICameraRepository
{
    /// <summary>
    /// Retrieves a camera entity by its unique identifier (local endpoint path).
    /// </summary>
    /// <param name="id">The camera identifier (local endpoint path).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The camera entity if found; otherwise, null.
    /// </returns>
    Task<CameraEntity?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a camera entity by its Google SDM device name.
    /// </summary>
    /// <param name="deviceName">The full SDM device resource name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The camera entity if found; otherwise, null.
    /// </returns>
    Task<CameraEntity?> GetByDeviceNameAsync(string deviceName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all camera entities.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A list of all camera entities.
    /// </returns>
    Task<List<CameraEntity>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all enabled camera entities.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A list of enabled camera entities.
    /// </returns>
    Task<List<CameraEntity>> GetEnabledAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all camera IDs.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A list of all camera IDs.
    /// </returns>
    Task<List<string>> GetAllIdsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if any cameras exist in the repository.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// True if at least one camera exists; otherwise, false.
    /// </returns>
    Task<bool> AnyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new camera entity to the repository.
    /// </summary>
    /// <param name="camera">The camera entity to add.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AddAsync(CameraEntity camera, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing camera entity.
    /// </summary>
    /// <param name="camera">The camera entity with updated values.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdateAsync(CameraEntity camera, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists all pending changes to the underlying data store.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The number of state entries written to the data store.
    /// </returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
