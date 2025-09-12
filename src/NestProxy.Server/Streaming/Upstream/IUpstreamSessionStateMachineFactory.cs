namespace NestProxy.Server.Streaming.Upstream;

/// <summary>
/// Factory abstraction for creating <see cref="IUpstreamSessionStateMachine"/> instances.
/// </summary>
public interface IUpstreamSessionStateMachineFactory
{
    /// <summary>
    /// Creates a new state machine instance for an upstream session.
    /// </summary>
    /// <param name="cameraId">Camera identifier for logging (local endpoint path).</param>
    /// <param name="instanceId">Session instance identifier (e.g., "primary", "secondary").</param>
    /// <returns>A new <see cref="IUpstreamSessionStateMachine"/> instance.</returns>
    IUpstreamSessionStateMachine Create(string cameraId, string instanceId);
}
