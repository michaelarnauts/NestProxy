using System.Text;

using NestProxy.Rtsp.Messages;
using NestProxy.Rtsp.Server;

namespace NestProxy.Server.Streaming.Downstream.Server.Dispatching;

/// <summary>
/// Default implementation of <see cref="IRtspResponseFormatter"/> that formats headers as semicolon-separated
/// key-value pairs and truncates body content for structured logging.
/// </summary>
public sealed class RtspResponseFormatter : IRtspResponseFormatter
{
    /// <inheritdoc />
    public string FormatRequestHeaders(RtspRequest request, params string[] headerKeys)
    {
        StringBuilder headers = new();
        foreach (string key in headerKeys)
        {
            if (request.Headers.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value))
            {
                headers.Append(key).Append(':').Append(' ').Append(value).Append("; ");
            }
        }

        if (headers.Length >= 2)
        {
            headers.Length -= 2;
        }

        return headers.ToString();
    }

    /// <inheritdoc />
    public string FormatResponseHeaders(RtspResponse response, params string[] headerKeys)
    {
        StringBuilder headers = new();
        foreach (string key in headerKeys)
        {
            if (response.Headers.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value))
            {
                headers.Append(key).Append(':').Append(' ').Append(value).Append("; ");
            }
        }

        if (headers.Length >= 2)
        {
            headers.Length -= 2;
        }

        return headers.ToString();
    }

    /// <inheritdoc />
    public string TruncateBody(ReadOnlyMemory<byte> data, int maxLength = 2000)
    {
        if (data.Length == 0)
        {
            return string.Empty;
        }

        int length = Math.Min(maxLength, data.Length);
        string body = Encoding.UTF8.GetString(data.Span[..length]);
        if (data.Length > maxLength)
        {
            body += "...";
        }

        return body;
    }

    /// <inheritdoc />
    public async Task SendSimpleOkAsync(RtspServerConnection connection, RtspRequest request, CancellationToken cancellationToken = default)
    {
        var response = request.CreateResponse();
        await connection.SendResponseAsync(response, cancellationToken).ConfigureAwait(false);
    }
}
