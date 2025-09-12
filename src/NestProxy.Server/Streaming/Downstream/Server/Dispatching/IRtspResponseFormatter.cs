using System.Threading;
using System.Threading.Tasks;
using NestProxy.Rtsp.Messages;

namespace NestProxy.Server.Streaming.Downstream.Server.Dispatching;

/// <summary>
/// Abstracts RTSP response formatting and logging operations to decouple dispatcher logic from
/// presentation concerns (header formatting, body truncation, structured logging).
/// </summary>
public interface IRtspResponseFormatter
{
    /// <summary>
    /// Formats a subset of request headers for structured logging.
    /// </summary>
    /// <param name="request">The RTSP request.</param>
    /// <param name="headerKeys">The header keys to include.</param>
    /// <returns>A formatted string containing the requested headers.</returns>
    string FormatRequestHeaders(RtspRequest request, params string[] headerKeys);

    /// <summary>
    /// Formats a subset of response headers for structured logging.
    /// </summary>
    /// <param name="response">The RTSP response.</param>
    /// <param name="headerKeys">The header keys to include.</param>
    /// <returns>A formatted string containing the requested headers.</returns>
    string FormatResponseHeaders(RtspResponse response, params string[] headerKeys);

    /// <summary>
    /// Truncates response body data for logging purposes.
    /// </summary>
    /// <param name="data">The response body.</param>
    /// <param name="maxLength">Maximum length before truncation.</param>
    /// <returns>A string representation of the (truncated) body.</returns>
    string TruncateBody(ReadOnlyMemory<byte> data, int maxLength = 2000);

    /// <summary>
    /// Sends a simple 200 OK response with no additional headers.
    /// </summary>
    /// <param name="connection">The downstream connection.</param>
    /// <param name="request">The original request.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task SendSimpleOkAsync(Rtsp.Server.RtspServerConnection connection, RtspRequest request, CancellationToken cancellationToken = default);
}
