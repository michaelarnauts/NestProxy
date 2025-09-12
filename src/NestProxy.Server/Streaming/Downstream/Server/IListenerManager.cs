using NestProxy.Rtsp.Server;

namespace NestProxy.Server.Streaming.Downstream.Server;

/// <summary>
/// Abstraction for managing the lifecycle of RTSP server connections.
/// </summary>
public interface IListenerManager
{
    /// <summary>
    /// Starts accepting RTSP clients asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for graceful shutdown.</param>
    /// <returns>A task representing the asynchronous accept loop.</returns>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the accept loop and disposes all connections.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for shutdown.</param>
    /// <returns>A task representing the asynchronous stop operation.</returns>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers a callback to be invoked when a new connection is accepted.
    /// </summary>
    /// <param name="onAccepted">The callback to invoke.</param>
    void RegisterOnAccepted(Func<RtspServerConnection, Task> onAccepted);
}
