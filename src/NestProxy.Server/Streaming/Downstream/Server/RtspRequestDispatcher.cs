using NestProxy.Rtsp.Messages;
using NestProxy.Rtsp.Server;

namespace NestProxy.Server.Streaming.Downstream.Server;

/// <summary>
/// Handles incoming RTSP requests from a downstream client (represented by <see cref="RtspServerConnection"/>) and
/// delegates session lifecycle operations to <see cref="SessionRegistry"/>. This dispatcher does not own
/// socket resources; instead it invokes callbacks to allocate transports and to remove connections/sessions.
/// </summary>
/// <param name="cameras">Shared camera registry keyed by camera path.</param>
/// <param name="sessions">Session manager used to create or refresh client sessions.</param>
/// <param name="responseFormatter">Response formatting utility.</param>
/// <param name="describeHandler">Handler for DESCRIBE requests.</param>
/// <param name="setupHandler">Handler for SETUP requests.</param>
/// <param name="playHandler">Handler for PLAY requests.</param>
/// <param name="teardownHandler">Handler for TEARDOWN requests.</param>
/// <param name="logger">Logger for structured RTSP request/response output.</param>
public sealed class RtspRequestDispatcher(
    CameraRegistry cameras,
    ISessionRegistry sessions,
    IRtspResponseFormatter responseFormatter,
    DescribeRequestHandler describeHandler,
    SetupRequestHandler setupHandler,
    PlayRequestHandler playHandler,
    TeardownRequestHandler teardownHandler,
    ILogger<RtspRequestDispatcher> logger) : IRtspRequestDispatcher
{
    /// <inheritdoc />
    public async Task DispatchAsync(RtspServerConnection connection, RtspRequest request, CancellationToken cancellationToken = default)
    {
        string logPrefix = BuildPrefixForConnection(connection, request);
        string reqHdrs = responseFormatter.FormatRequestHeaders(request, "CSeq", "Session", "Transport", "Range", "Scale", "Accept", "Authorization", "Content-Type", "Content-Base", "Content-Length");

        try
        {
            switch (request.Method)
            {
                case "OPTIONS":
                    logger.LogInformation("{Prefix} Received OPTIONS {Uri} headers: {Headers}.", logPrefix, request.Uri, reqHdrs);
                    sessions.RefreshActivityForConnection(connection);
                    await responseFormatter.SendSimpleOkAsync(connection, request, cancellationToken).ConfigureAwait(false);
                    return;

                case "GET_PARAMETER":
                    logger.LogInformation("{Prefix} Received GET_PARAMETER {Uri} headers: {Headers}.", logPrefix, request.Uri, reqHdrs);
                    sessions.RefreshActivityForConnection(connection);
                    await responseFormatter.SendSimpleOkAsync(connection, request, cancellationToken).ConfigureAwait(false);
                    return;

                case "DESCRIBE":
                    logger.LogInformation("{Prefix} Received DESCRIBE {Uri} headers: {Headers}.", logPrefix, request.Uri, reqHdrs);
                    await describeHandler.HandleAsync(connection, request, logPrefix, cancellationToken).ConfigureAwait(false);
                    return;

                case "SETUP":
                    logger.LogInformation("{Prefix} Received SETUP {Uri} headers: {Headers}.", logPrefix, request.Uri, reqHdrs);
                    sessions.RefreshActivityForConnection(connection);
                    await setupHandler.HandleAsync(connection, request, logPrefix, cancellationToken).ConfigureAwait(false);
                    return;

                case "PLAY":
                    logger.LogInformation("{Prefix} Received PLAY {Uri} headers: {Headers}.", logPrefix, request.Uri, reqHdrs);
                    sessions.RefreshActivityForConnection(connection);
                    await playHandler.HandleAsync(connection, request, logPrefix, cancellationToken).ConfigureAwait(false);
                    return;

                case "TEARDOWN":
                    await teardownHandler.HandleAsync(connection, request, logPrefix, cancellationToken).ConfigureAwait(false);
                    return;

                default:
                    logger.LogInformation("{Prefix} Received {Method} {Uri} headers: {Headers}.", logPrefix, request.Method, request.Uri, reqHdrs);
                    sessions.RefreshActivityForConnection(connection);
                    await responseFormatter.SendSimpleOkAsync(connection, request, cancellationToken).ConfigureAwait(false);
                    return;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{Prefix} Dispatching {Method} request failed: {ErrorMessage}", logPrefix, request.Method, ex.Message);
        }
    }

    /// <inheritdoc />
    public void RegisterOnRemoveConnection(Func<RtspServerConnection, Task> onRemoveConnection)
    {
        teardownHandler.RegisterOnRemoveConnection(onRemoveConnection);
    }

    private string BuildPrefixForConnection(RtspServerConnection connection, RtspRequest request)
    {
        string sessionId = request.Session?[..Math.Min(8, request.Session.Length)] ?? "?";
        string cameraId = "?";

        var camera = cameras.FindCameraByConnection(connection);
        if (camera != null)
        {
            cameraId = camera.CameraId;
        }

        return $"[{cameraId}] [client-{sessionId}]";
    }
}