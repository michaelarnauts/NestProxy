using NestProxy.Rtsp.Messages;
using NestProxy.Rtsp.Server;

namespace NestProxy.Server.Streaming.Downstream.Server.Dispatching.Handlers;

/// <summary>
/// Handles RTSP TEARDOWN requests by removing the associated session (if Session header is present)
/// or the entire connection (if no Session header). Delegates session removal to <see cref="ISessionRegistry"/>
/// for proper resource cleanup and disposal.
/// </summary>
/// <param name="sessionManager">Session registry for managing client session lifecycle.</param>
/// <param name="responseFormatter">Response formatting utility.</param>
/// <param name="logger">Structured logger.</param>
public sealed class TeardownRequestHandler(
    ISessionRegistry sessionManager,
    IRtspResponseFormatter responseFormatter,
    ILogger<TeardownRequestHandler> logger) : IRtspRequestHandler
{
    private Func<RtspServerConnection, Task>? _onRemoveConnection;

    /// <summary>
    /// Registers a callback to be invoked when a connection is removed.
    /// </summary>
    public void RegisterOnRemoveConnection(Func<RtspServerConnection, Task> onRemoveConnection)
    {
        _onRemoveConnection = onRemoveConnection;
    }

    /// <inheritdoc />
    public async Task HandleAsync(RtspServerConnection connection, RtspRequest request, string logPrefix, CancellationToken cancellationToken = default)
    {
        if (request.Headers.TryGetValue("Session", out string? sessionId) && !string.IsNullOrWhiteSpace(sessionId))
        {
            // Delegate session removal to SessionManager for proper cleanup.
            sessionManager.RemoveSessionById(sessionId);
        }
        else
        {
            // No session ID ? remove all sessions for this connection.
            sessionManager.RemoveSessionsForConnection(connection);
            if (_onRemoveConnection != null)
            {
                await _onRemoveConnection.Invoke(connection).ConfigureAwait(false);
            }
        }

        RtspResponse response = request.CreateResponse();
        string headers = responseFormatter.FormatResponseHeaders(response, "CSeq", "Session", "Transport", "Content-Type", "Content-Base", "Content-Length");
        logger.LogInformation("{Prefix} Received TEARDOWN {Code} headers: {Headers}.", logPrefix, response.StatusCode, headers);
        await connection.SendResponseAsync(response, cancellationToken).ConfigureAwait(false);
    }
}
