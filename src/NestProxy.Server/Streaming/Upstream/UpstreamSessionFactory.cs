using System.Net.Security;

using Microsoft.Extensions.Options;

namespace NestProxy.Server.Streaming.Upstream;

/// <summary>
/// Default implementation of <see cref="IUpstreamSessionFactory"/> that creates <see cref="UpstreamSession"/> instances
/// with injected dependencies for logging and message formatting.
/// </summary>
/// <param name="logger">Logger instance for upstream session operations.</param>
/// <param name="formatter">RTSP message formatter for structured logging.</param>
/// <param name="stateMachineFactory">Factory for creating state machine instances.</param>
/// <param name="statisticsFactory">Factory for creating statistics tracker instances.</param>
/// <param name="options">Streaming options for channel capacity configuration.</param>
public sealed class UpstreamSessionFactory(
    ILogger<UpstreamSession> logger,
    IRtspResponseFormatter formatter,
    IUpstreamSessionStateMachineFactory stateMachineFactory,
    IUpstreamSessionStatisticsFactory statisticsFactory,
    IOptions<StreamingOptions> options) : IUpstreamSessionFactory
{
    /// <inheritdoc />
    public IUpstreamSession Create(string cameraId, RemoteCertificateValidationCallback certValidator, string instanceId)
    {
        // Create new state machine and statistics instances for this session using injected factories
        IUpstreamSessionStateMachine stateMachine = stateMachineFactory.Create(cameraId, instanceId);
        IUpstreamSessionStatistics statistics = statisticsFactory.Create(cameraId, instanceId);

        int packetCapacity = options.Value.PacketChannelCapacity;
        return new UpstreamSession(cameraId, logger, certValidator, instanceId, formatter, stateMachine, statistics, packetCapacity);
    }
}
