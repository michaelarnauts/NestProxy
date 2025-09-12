namespace NestProxy.Server.Core;

/// <summary>
/// Factory abstraction for creating <see cref="CameraProxyRunner"/> instances for a given camera.
/// </summary>
public interface ICameraProxyRunnerFactory
{
    /// <summary>
    /// Creates a new <see cref="CameraProxyRunner"/> for the specified camera identifier.
    /// </summary>
    /// <param name="cameraId">The unique identifier of the camera (local endpoint path).</param>
    /// <returns>A new <see cref="CameraProxyRunner"/> instance.</returns>
    CameraProxyRunner Create(string cameraId);
}