namespace NestProxy.Server.Streaming.Upstream;

/// <summary>
/// Default implementation of <see cref="IUpstreamSessionStateMachineFactory"/> that creates
/// <see cref="UpstreamSessionStateMachine"/> instances with logger injection.
/// </summary>
/// <param name="logger">Logger instance for state machine operations.</param>
public sealed class UpstreamSessionStateMachineFactory(ILogger<UpstreamSessionStateMachine> logger) : IUpstreamSessionStateMachineFactory
{
    private readonly ILogger<UpstreamSessionStateMachine> _logger = logger;

    /// <inheritdoc />
    public IUpstreamSessionStateMachine Create(string cameraId, string instanceId)
    {
        return new UpstreamSessionStateMachine(cameraId, instanceId, _logger);
    }
}
