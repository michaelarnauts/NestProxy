using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace NestProxy.Server.Streaming.Upstream;

/// <summary>
/// Factory abstraction for creating <see cref="IUpstreamSession"/> instances.
/// Enables dependency injection and testability by decoupling session creation from orchestrator logic.
/// </summary>
public interface IUpstreamSessionFactory
{
    /// <summary>
    /// Creates a new upstream RTSP session instance.
    /// </summary>
    /// <param name="cameraId">Camera identifier for logging (local endpoint path).</param>
    /// <param name="certValidator">Certificate validation callback for RTSPS connections.</param>
    /// <param name="instanceId">Session instance identifier (e.g., "primary", "secondary").</param>
    /// <returns>A new <see cref="IUpstreamSession"/> instance.</returns>
    IUpstreamSession Create(string cameraId, RemoteCertificateValidationCallback certValidator, string instanceId);
}
