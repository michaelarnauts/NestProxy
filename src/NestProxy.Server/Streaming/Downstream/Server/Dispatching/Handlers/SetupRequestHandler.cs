using NestProxy.Rtsp.Messages;
using NestProxy.Rtsp.Server;

namespace NestProxy.Server.Streaming.Downstream.Server.Dispatching.Handlers;

/// <summary>
/// Handles RTSP SETUP requests by parsing the requested track control path, looking up payload type,
/// delegating transport negotiation to <see cref="ITransportSetupService"/>, and sending the response.
/// </summary>
/// <param name="cameras">Registry of registered camera streams.</param>
/// <param name="sessions">Session registry for creating or retrieving client sessions.</param>
/// <param name="transportSetup">Transport setup service.</param>
/// <param name="controlMapResolver">Service to resolve control paths to payload types.</param>
/// <param name="responseFormatter">Response formatting utility.</param>
/// <param name="logger">Structured logger.</param>
public sealed class SetupRequestHandler(
    CameraRegistry cameras,
    ISessionRegistry sessions,
    ITransportSetupService transportSetup,
    IControlMapResolver controlMapResolver,
    IRtspResponseFormatter responseFormatter,
    ILogger<SetupRequestHandler> logger) : IRtspRequestHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(RtspServerConnection connection, RtspRequest request, string logPrefix, CancellationToken cancellationToken = default)
    {
        string path = request.Uri?.AbsolutePath.TrimStart('/') ?? string.Empty;
        string[] segments = path.Split('/', 2);
        path = segments[0]; // ignore any trailing /trackID=xx
        string control = segments.Length > 1 ? segments[1] : string.Empty;

        if (!cameras.TryGetValue(path, out CameraStream? camera))
        {
            RtspResponse notFound = request.CreateResponse();
            notFound.StatusCode = RtspStatusCode.NotFound;
            logger.LogWarning("{Prefix} SETUP 404 Not Found for path '{Path}'.", logPrefix, path);
            await connection.SendResponseAsync(notFound, cancellationToken).ConfigureAwait(false);
            return;
        }

        int? payloadTypeForThisSetup = controlMapResolver.Resolve(path, control);

        // If no specific track control was resolved, this is a session-level SETUP
        // Pass the camera so transport setup can create bindings for all payload types
        bool isSessionLevelSetup = string.IsNullOrEmpty(control) || payloadTypeForThisSetup == null;

        ClientSession session = sessions.GetOrCreateSession(camera, connection);

        try
        {
            await transportSetup.HandleSetupAsync(connection, request, session, camera, payloadTypeForThisSetup, isSessionLevelSetup, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            RtspResponse error = request.CreateResponse();
            error.StatusCode = RtspStatusCode.InternalServerError;
            string headers = responseFormatter.FormatResponseHeaders(error, "CSeq", "Session");
            logger.LogError("{Prefix} Exception handling SETUP, sending 500. Headers: {Headers}. Error={Error}",
                logPrefix, headers, ex.Message);
            await connection.SendResponseAsync(error, cancellationToken).ConfigureAwait(false);
        }
    }
}
