using System.Text;

using NestProxy.Rtsp.Messages;
using NestProxy.Rtsp.Server;

namespace NestProxy.Server.Streaming.Downstream.Server.Dispatching.Handlers;

/// <summary>
/// Handles RTSP DESCRIBE requests by synthesizing downstream SDP from upstream camera metadata
/// and returning it as application/sdp with proper Content-Base header.
/// </summary>
/// <param name="cameras">Registry of registered camera streams.</param>
/// <param name="describeSynthesizer">SDP synthesis service.</param>
/// <param name="responseFormatter">Response formatting utility.</param>
/// <param name="logger">Structured logger.</param>
public sealed class DescribeRequestHandler(
    CameraRegistry cameras,
    IDescribeSynthesizer describeSynthesizer,
    IRtspResponseFormatter responseFormatter,
    ILogger<DescribeRequestHandler> logger) : IRtspRequestHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(RtspServerConnection connection, RtspRequest request, string logPrefix, CancellationToken cancellationToken = default)
    {
        string path = request.Uri?.AbsolutePath.TrimStart('/') ?? string.Empty;
        if (!cameras.TryGetValue(path, out CameraStream? camera))
        {
            RtspResponse notFound = request.CreateResponse();
            notFound.StatusCode = RtspStatusCode.NotFound;
            logger.LogWarning("{Prefix} DESCRIBE 404 Not Found for path '{Path}'.", logPrefix, path);
            await connection.SendResponseAsync(notFound, cancellationToken).ConfigureAwait(false);
            return;
        }

        string? sdp = describeSynthesizer.Build(camera);
        if (sdp == null)
        {
            RtspResponse notReady = request.CreateResponse();
            notReady.StatusCode = RtspStatusCode.MethodNotValidInThisState;
            logger.LogWarning("{Prefix} DESCRIBE 455 Not Ready (no SDP) for path '{Path}'.", logPrefix, path);
            await connection.SendResponseAsync(notReady, cancellationToken).ConfigureAwait(false);
            return;
        }

        byte[] sdpBytes = Encoding.ASCII.GetBytes(sdp);
        RtspResponse response = request.CreateResponse();
        response.Headers["Content-Type"] = "application/sdp";
        response.Headers["Content-Base"] = request.Uri?.ToString() ?? string.Empty;
        response.Data = sdpBytes;
        response.ContentLength = sdpBytes.Length;

        string headers = responseFormatter.FormatResponseHeaders(response, "CSeq", "Content-Type", "Content-Base", "Content-Length");
        logger.LogInformation("{Prefix} Sending DESCRIBE {Code} headers: {Headers} body:\n{Body}.",
            logPrefix, response.StatusCode, headers, responseFormatter.TruncateBody(response.Data));
        await connection.SendResponseAsync(response, cancellationToken).ConfigureAwait(false);
    }
}
