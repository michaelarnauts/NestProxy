using System.Buffers;
using System.Text;

using NestProxy.Rtsp.Messages;

namespace NestProxy.Rtsp.Protocol;

/// <summary>
/// Serializes RTSP messages and interleaved frames to a stream.
/// </summary>
public class RtspMessageSerializer : IRtspMessageSerializer
{
    private static readonly byte[] CrLf = [(byte)'\r', (byte)'\n'];
    private static readonly byte[] ColonSpace = [(byte)':', (byte)' '];

    /// <inheritdoc />
    public async Task SerializeAsync(RtspRequest request, Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(stream);

        await WriteStartLineAndHeadersAsync(request.Method, request.RtspUri, request.Version, request, stream, cancellationToken);

        if (request.Data.Length > 0)
        {
            await stream.WriteAsync(request.Data, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task SerializeAsync(RtspResponse response, Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(stream);

        await WriteStatusLineAndHeadersAsync(response.Version, response.ReturnCode, response.ReasonPhrase, response, stream, cancellationToken);

        if (response.Data.Length > 0)
        {
            await stream.WriteAsync(response.Data, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task SerializeAsync(InterleavedFrame frame, Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        // Interleaved frame format: $ + channel (1 byte) + length (2 bytes big-endian) + data
        byte[] header =
        [
            (byte)'$',
            frame.Channel,
            (byte)(frame.Data.Length >> 8),
            (byte)(frame.Data.Length & 0xFF)
        ];

        await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(frame.Data, cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteStartLineAndHeadersAsync(string method, Uri uri, string version, RtspMessage message, Stream stream, CancellationToken cancellationToken)
    {
        // Request line: METHOD URI RTSP/1.0\r\n
        ArrayBufferWriter<byte> writer = new();
        WriteString(writer, method);
        WriteSpace(writer);
        WriteString(writer, uri.ToString());
        WriteSpace(writer);
        WriteString(writer, version);
        WriteNewline(writer);

        await WriteHeadersAsync(message, stream, writer, cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteStatusLineAndHeadersAsync(string version, int returnCode, string reasonPhrase, RtspMessage message, Stream stream, CancellationToken cancellationToken)
    {
        // Status line: RTSP/1.0 CODE REASON\r\n
        ArrayBufferWriter<byte> writer = new();
        WriteString(writer, version);
        WriteSpace(writer);
        WriteString(writer, returnCode.ToString());
        WriteSpace(writer);
        WriteString(writer, reasonPhrase);
        WriteNewline(writer);

        await WriteHeadersAsync(message, stream, writer, cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteHeadersAsync(RtspMessage message, Stream stream, ArrayBufferWriter<byte> writer, CancellationToken cancellationToken)
    {
        // Ensure required headers are present.
        if (!message.Headers.ContainsKey("CSeq"))
        {
            message.Headers["CSeq"] = message.CSeq.ToString();
        }

        if (message.Data.Length > 0 && !message.Headers.ContainsKey("Content-Length"))
        {
            message.Headers["Content-Length"] = message.Data.Length.ToString();
        }

        // Write all headers.
        foreach (var header in message.Headers)
        {
            WriteString(writer, header.Key);
            WriteColonSpace(writer);
            WriteString(writer, header.Value);
            WriteNewline(writer);
        }

        // Empty line to separate headers from body.
        WriteNewline(writer);

        await stream.WriteAsync(writer.WrittenMemory, cancellationToken).ConfigureAwait(false);
    }

    private static void WriteString(ArrayBufferWriter<byte> writer, string text)
    {
        ReadOnlySpan<char> span = text.AsSpan();
        int maxByteCount = Encoding.UTF8.GetMaxByteCount(span.Length);
        Span<byte> destination = writer.GetSpan(maxByteCount);
        int bytesWritten = Encoding.UTF8.GetBytes(span, destination);
        writer.Advance(bytesWritten);
    }

    private static void WriteNewline(ArrayBufferWriter<byte> writer)
    {
        Span<byte> newline = writer.GetSpan(2);
        CrLf.CopyTo(newline);
        writer.Advance(2);
    }

    private static void WriteSpace(ArrayBufferWriter<byte> writer)
    {
        Span<byte> space = writer.GetSpan(1);
        space[0] = (byte)' ';
        writer.Advance(1);
    }

    private static void WriteColonSpace(ArrayBufferWriter<byte> writer)
    {
        Span<byte> colon = writer.GetSpan(2);
        ColonSpace.CopyTo(colon);
        writer.Advance(2);
    }
}
