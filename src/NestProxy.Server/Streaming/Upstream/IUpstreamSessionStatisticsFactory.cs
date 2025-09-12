namespace NestProxy.Server.Streaming.Upstream;

/// <summary>
/// Factory abstraction for creating <see cref="IUpstreamSessionStatistics"/> instances.
/// </summary>
public interface IUpstreamSessionStatisticsFactory
{
    /// <summary>
    /// Creates a new statistics tracker instance for an upstream session.
    /// </summary>
    /// <param name="cameraId">Camera identifier for logging (local endpoint path).</param>
    /// <param name="instanceId">Session instance identifier (e.g., "primary", "secondary").</param>
    /// <returns>A new <see cref="IUpstreamSessionStatistics"/> instance.</returns>
    IUpstreamSessionStatistics Create(string cameraId, string instanceId);
}
