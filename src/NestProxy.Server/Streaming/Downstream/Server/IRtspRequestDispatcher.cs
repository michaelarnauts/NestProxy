using NestProxy.Rtsp.Messages;
using NestProxy.Rtsp.Server;

namespace NestProxy.Server.Streaming.Downstream.Server;

/// <summary>
/// Defines a contract for dispatching RTSP requests to appropriate handlers and managing connection removal
/// notifications.
/// </summary>
public interface IRtspRequestDispatcher
{
    /// <summary>
    /// Dispatches a single RTSP <paramref name="request"/> originating from <paramref name="connection"/> to the
    /// appropriate handler (OPTIONS, GET_PARAMETER, DESCRIBE, SETUP, PLAY, TEARDOWN or default OK). The method
    /// also refreshes session activity timestamps for keep-alive semantics.
    /// </summary>
    /// <param name="connection">The client RTSP connection (TCP connection) from which the request arrived.</param>
    /// <param name="request">The parsed RTSP request message.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task DispatchAsync(RtspServerConnection connection, RtspRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers a callback to be invoked when a connection is removed.
    /// </summary>
    /// <param name="onRemoveConnection">The callback to invoke.</param>
    void RegisterOnRemoveConnection(Func<RtspServerConnection, Task> onRemoveConnection);
}