using Microsoft.Extensions.Options;

namespace NestProxy.Server.Core;

/// <summary>
/// Factory for creating <see cref="CameraProxyRunner"/> instances (one per camera) used by the
/// <see cref="CameraSupervisorService"/>.
/// </summary>
public class CameraProxyRunnerFactory(
    IServiceScopeFactory scopeFactory,
    IOptions<StreamingOptions> options,
    ICameraRuntimeState runtimeState,
    ILogger<CameraProxyRunner> logger) : ICameraProxyRunnerFactory
{
    /// <summary>
    /// Creates a new runner for the specified camera id (local endpoint path).
    /// </summary>
    public CameraProxyRunner Create(string cameraId)
    {
        return new CameraProxyRunner(cameraId, scopeFactory, options, runtimeState, logger);
    }
}