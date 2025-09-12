using System.Collections.Concurrent;

namespace NestProxy.Server.Core;

/// <summary>
/// Maintains runtime camera relay state for quick lookup by controllers and services.
/// </summary>
public class CameraRuntimeState : ICameraRuntimeState
{
    private readonly ConcurrentDictionary<string, byte> _running = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public void SetRunning(string cameraId, bool isRunning)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cameraId);
        if (isRunning)
        {
            _running[cameraId] = 1;
        }
        else
        {
            _running.TryRemove(cameraId, out _);
        }
    }

    /// <inheritdoc />
    public bool IsRunning(string cameraId)
    {
        return !string.IsNullOrWhiteSpace(cameraId) && _running.ContainsKey(cameraId);
    }

    /// <inheritdoc />
    public IReadOnlyCollection<string> RunningCameraIds => [.. _running.Keys];

    /// <inheritdoc />
    public int RunningCount => _running.Count;
}
