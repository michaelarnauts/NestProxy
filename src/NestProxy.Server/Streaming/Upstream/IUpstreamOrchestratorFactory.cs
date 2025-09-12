using NestProxy.Server.Core;

namespace NestProxy.Server.Streaming.Upstream;

/// <summary>
/// Factory abstraction for creating <see cref="ICameraRelay"/> (upstream orchestrator) instances so that
/// DI can inject different relay strategies (e.g. test doubles) without modifying the runner logic.
/// </summary>
public interface IUpstreamOrchestratorFactory
{
    /// <summary>
    /// Creates a new orchestrator / relay for the specified camera identifier.
    /// </summary>
    /// <param name="cameraId">Camera identifier (local endpoint path).</param>
    /// <returns>Newly created relay instance.</returns>
    ICameraRelay Create(string cameraId);
}