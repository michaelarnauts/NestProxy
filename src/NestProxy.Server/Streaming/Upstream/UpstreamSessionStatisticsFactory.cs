namespace NestProxy.Server.Streaming.Upstream;

/// <summary>
/// Default implementation of <see cref="IUpstreamSessionStatisticsFactory"/> that creates
/// <see cref="UpstreamSessionStatistics"/> instances with logger injection.
/// </summary>
/// <param name="logger">Logger instance for statistics operations.</param>
public sealed class UpstreamSessionStatisticsFactory(ILogger<UpstreamSessionStatistics> logger) : IUpstreamSessionStatisticsFactory
{
    private readonly ILogger<UpstreamSessionStatistics> _logger = logger;

    /// <inheritdoc />
    public IUpstreamSessionStatistics Create(string cameraId, string instanceId)
    {
        return new UpstreamSessionStatistics(cameraId, instanceId, _logger);
    }
}
