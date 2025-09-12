using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

using NestProxy.Rtsp.Server;

namespace NestProxy.Server.Core;

/// <summary>
/// Encapsulates the registry of camera streams, providing thread-safe access and update methods.
/// </summary>
public class CameraRegistry : IEnumerable<CameraStream>
{
    private readonly ConcurrentDictionary<string, CameraStream> _cameras = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Attempts to retrieve the camera stream associated with the specified path.
    /// </summary>
    /// <param name="path">The unique identifier or path used to locate the camera stream.</param>
    /// <param name="camera">
    /// When this method returns, contains the camera stream associated with the specified path,
    /// or null if the camera was not found.
    /// </param>
    /// <returns>true if a camera stream with the specified path is found; otherwise, false.</returns>
    public bool TryGetValue(string path, [MaybeNullWhen(false)] out CameraStream camera)
        => _cameras.TryGetValue(path, out camera);

    /// <summary>
    /// Determines whether a camera with the specified path exists in the registry.
    /// </summary>
    /// <param name="path">The unique path identifying the camera to locate. Cannot be null.</param>
    /// <returns><see langword="true"/> if a camera with the specified path exists; otherwise, <see langword="false"/>.</returns>
    public bool Exists(string path)
        => _cameras.ContainsKey(path);

    /// <summary>
    /// Gets or sets the camera stream associated with the specified path.
    /// </summary>
    /// <param name="path">The unique path identifying the camera stream to get or set. Cannot be null.</param>
    /// <returns>The camera stream associated with the specified path.</returns>
    public CameraStream this[string path]
    {
        get => _cameras[path];
        set => _cameras[path] = value;
    }

    /// <summary>
    /// Adds a new camera stream for the specified path or updates the existing stream if the path already exists.
    /// </summary>
    /// <remarks>
    /// If a camera stream already exists for the given path, it will be replaced with the provided
    /// stream. This method is thread-safe.
    /// </remarks>
    /// <param name="path">The unique identifier for the camera stream. Cannot be null or empty.</param>
    /// <param name="camera">The camera stream to associate with the specified path. Cannot be null.</param>
    public void AddOrUpdate(string path, CameraStream camera)
        => _cameras.AddOrUpdate(path, camera, (_, __) => camera);

    /// <summary>
    /// Gets a collection containing the unique paths for all configured cameras.
    /// </summary>
    public ICollection<string> Paths
        => _cameras.Keys;

    /// <summary>
    /// Finds a camera stream associated with the specified RTSP connection by searching through all
    /// active client sessions.
    /// </summary>
    /// <remarks>
    /// This method iterates through all registered cameras and their active sessions, using appropriate
    /// locking to ensure thread safety. This operation is O(N*M) where N is cameras and M is sessions.
    /// </remarks>
    /// <param name="connection">The RTSP connection to search for.</param>
    /// <returns>The camera stream if found; otherwise, null.</returns>
    public CameraStream? FindCameraByConnection(RtspServerConnection connection)
    {
        foreach (CameraStream camera in _cameras.Values)
        {
            bool found;
            lock (camera.Sessions)
            {
                found = camera.Sessions.Any(s => s.Connection == connection);
            }

            if (found)
            {
                return camera;
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the collection of camera streams currently managed by the system.
    /// </summary>
    public ICollection<CameraStream> Streams
        => _cameras.Values;

    /// <summary>
    /// Returns an enumerator that iterates through the collection of camera streams.
    /// </summary>
    /// <returns>An enumerator that can be used to iterate through the collection of <see cref="CameraStream"/> objects.</returns>
    public IEnumerator<CameraStream> GetEnumerator()
        => _cameras.Values.GetEnumerator();

    /// <summary>
    /// Returns an enumerator that iterates through the collection.
    /// </summary>
    /// <returns>An enumerator that can be used to iterate through the collection.</returns>
    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();
}
