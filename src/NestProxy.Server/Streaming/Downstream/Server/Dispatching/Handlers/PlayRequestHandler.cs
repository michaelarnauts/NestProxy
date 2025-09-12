using NestProxy.Rtsp.Messages;
using NestProxy.Rtsp.Server;

namespace NestProxy.Server.Streaming.Downstream.Server.Dispatching.Handlers;

/// <summary>
/// Handles RTSP PLAY requests by marking the client session as playing and returning 200 OK.
/// </summary>
/// <param name="cameras">Registry of registered camera streams.</param>
/// <param name="responseFormatter">Response formatting utility.</param>
public sealed class PlayRequestHandler(
    CameraRegistry cameras,
    IRtspResponseFormatter responseFormatter) : IRtspRequestHandler
{
    /// <inheritdoc />
    public Task HandleAsync(RtspServerConnection connection, RtspRequest request, string logPrefix, CancellationToken cancellationToken = default)
    {
        MarkPlaying(connection);
        return responseFormatter.SendSimpleOkAsync(connection, request, cancellationToken);
    }

    private void MarkPlaying(RtspServerConnection connection)
    {
        foreach (CameraStream camera in cameras)
        {
            lock (camera.Sessions)
            {
                foreach (ClientSession session in camera.Sessions.Where(s => s.Connection == connection))
                {
                    session.IsPlaying = true;
                    session.LastActivityUtc = DateTime.UtcNow;
                }
            }
        }
    }
}
