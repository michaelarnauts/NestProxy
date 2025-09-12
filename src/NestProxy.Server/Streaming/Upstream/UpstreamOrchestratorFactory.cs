using Microsoft.Extensions.Options;

using NestProxy.Server.Streaming.Downstream.Server;

namespace NestProxy.Server.Streaming.Upstream;

/// <summary>
/// Provides a factory for creating upstream orchestrator instances that manage camera relays for streaming operations.
/// </summary>
/// <param name="logger">The logger used to record diagnostic and operational information for upstream orchestrator instances.</param>
/// <param name="localServer">The local RTSP server used to facilitate streaming connections for camera relays.</param>
/// <param name="bindingSelector">The binding selector used to manage SSRC to track binding associations.</param>
/// <param name="options">The streaming options configuration that controls behavior and settings for upstream orchestrator instances.</param>
/// <param name="sessionFactory">The factory for creating upstream session instances.</param>
public class UpstreamOrchestratorFactory(
    ILogger<UpstreamOrchestrator> logger,
    ILocalRtspServer localServer,
    BindingSelector bindingSelector,
    IOptions<StreamingOptions> options,
    IUpstreamSessionFactory sessionFactory) : IUpstreamOrchestratorFactory
{
    /// <inheritdoc />
    public ICameraRelay Create(string cameraId)
    {
        return new UpstreamOrchestrator(cameraId, logger, localServer, bindingSelector, options, sessionFactory);
    }
}