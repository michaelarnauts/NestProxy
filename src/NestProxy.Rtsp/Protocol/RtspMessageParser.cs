using System.Buffers.Binary;
using System.Text;

using NestProxy.Rtsp.Buffers;
using NestProxy.Rtsp.Messages;

namespace NestProxy.Rtsp.Protocol;

/// <summary>
/// Parses RTSP messages and interleaved frames from a stream.
/// Handles both text-based RTSP messages and binary interleaved frames ($).
/// </summary>
/// <param name="bufferPool">Optional buffer pool for interleaved frame payloads.</param>
public class RtspMessageParser(LargeBufferPool? bufferPool = null) : IRtspMessageParser
{
    private const int MaxContentLength = 2 * 1024 * 1024; // 2MB safety cap
    private const int ReadChunkSize = 64 * 1024;

    private readonly LargeBufferPool _bufferPool = bufferPool ?? new LargeBufferPool();
    private readonly byte[] _buffer = GC.AllocateUninitializedArray<byte>(MaxContentLength);

    private int _bufferStart;
    private int _bufferEnd;

    private byte[]? _interleavedBuffer;
    private RtspParserState _state = RtspParserState.WaitingForMessage;
    private RtspMessage? _currentMessage;
    private int _contentLength;
    private byte _interleavedChannel;
    private int _interleavedLength;

    /// <inheritdoc />
    public RtspParserState State => _state;

    /// <inheritdoc />
    public void Reset()
    {
        ResetInternal(preserveBufferedData: false);
    }

    private void ResetInternal(bool preserveBufferedData)
    {
        ResetInterleavedBuffer();

        bool preserveRemaining = preserveBufferedData && _bufferStart != _bufferEnd;
        if (preserveRemaining)
        {
            CompactBuffer();
        }
        else
        {
            _bufferStart = 0;
            _bufferEnd = 0;
        }

        _state = RtspParserState.WaitingForMessage;
        _currentMessage = null;
        _contentLength = 0;
        _interleavedChannel = 0;
        _interleavedLength = 0;
    }

    /// <inheritdoc />
    public async Task<object?> ParseAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        while (true)
        {
            switch (_state)
            {
                case RtspParserState.WaitingForMessage:
                    await DetectMessageTypeAsync(stream, cancellationToken);
                    break;

                case RtspParserState.ReadingStartLine:
                    string? startLine = await ReadLineStringAsync(stream, cancellationToken);
                    if (startLine == null || _state == RtspParserState.Error)
                    {
                        return null;
                    }

                    ParseStartLine(startLine);
                    break;

                case RtspParserState.ReadingHeaders:
                    string? headerLine = await ReadLineStringAsync(stream, cancellationToken);
                    if (headerLine == null || _state == RtspParserState.Error)
                    {
                        return null;
                    }

                    bool headersComplete = ParseHeaderLine(headerLine);
                    if (_state == RtspParserState.Error)
                    {
                        return null;
                    }

                    if (headersComplete)
                    {
                        _state = _contentLength > 0 ? RtspParserState.ReadingBody : RtspParserState.MessageComplete;
                    }

                    break;

                case RtspParserState.ReadingBody:
                    if (!await ReadBodyAsync(stream, cancellationToken))
                    {
                        return null;
                    }

                    _state = RtspParserState.MessageComplete;
                    break;

                case RtspParserState.ReadingInterleavedFrame:
                    return await ReadInterleavedFrameAsync(stream, cancellationToken);

                case RtspParserState.MessageComplete:
                    {
                        RtspMessage? message = _currentMessage;
                        ResetInternal(preserveBufferedData: true);
                        return message;
                    }

                case RtspParserState.Error:
                    ResetInternal(preserveBufferedData: false);
                    throw new InvalidDataException("RTSP parser encountered invalid data");

                default:
                    throw new InvalidOperationException($"Unknown parser state: {_state}");
            }
        }
    }

    private void ResetInterleavedBuffer()
    {
        if (_interleavedBuffer != null)
        {
            _bufferPool.Return(_interleavedBuffer);
            _interleavedBuffer = null;
        }
    }

    private async Task DetectMessageTypeAsync(Stream stream, CancellationToken cancellationToken)
    {
        if (!await EnsureAvailableAsync(stream, 1, cancellationToken))
        {
            _state = RtspParserState.Error;
            return;
        }

        byte firstByte = _buffer[_bufferStart];

        if (firstByte == '$')
        {
            _bufferStart++;
            _state = RtspParserState.ReadingInterleavedFrame;
        }
        else
        {
            _state = RtspParserState.ReadingStartLine;
        }
    }

    private async Task<string?> ReadLineStringAsync(Stream stream, CancellationToken cancellationToken)
    {
        while (true)
        {
            if (TryReadLine(out ReadOnlySpan<byte> line))
            {
                return line.Length == 0 ? string.Empty : Encoding.UTF8.GetString(line);
            }

            if (_state == RtspParserState.Error)
            {
                return null;
            }

            // Line not complete, read more data.
            if (!await FillBufferAsync(stream, cancellationToken))
            {
                return null;
            }
        }
    }

    private bool TryReadLine(out ReadOnlySpan<byte> line)
    {
        int scan = _bufferStart;
        while (scan + 1 < _bufferEnd)
        {
            // Scan for CRLF in buffer to indicate end of line.
            if (_buffer[scan] == '\r' && _buffer[scan + 1] == '\n')
            {
                int length = scan - _bufferStart;
                line = new ReadOnlySpan<byte>(_buffer, _bufferStart, length);
                _bufferStart = scan + 2;

                if (_bufferStart == _bufferEnd)
                {
                    _bufferStart = 0;
                    _bufferEnd = 0;
                }

                return true;
            }

            scan++;
            if (scan - _bufferStart >= MaxContentLength)
            {
                _state = RtspParserState.Error;
                break;
            }
        }

        line = default;
        return false;
    }

    private void ParseStartLine(string line)
    {
        string[] parts = line.Split(' ', 3);
        if (parts.Length < 3)
        {
            _state = RtspParserState.Error;
            return;
        }

        if (parts[0].StartsWith("RTSP/", StringComparison.OrdinalIgnoreCase))
        {
            _currentMessage = new RtspResponse
            {
                Version = parts[0],
                ReturnCode = int.Parse(parts[1]),
                ReasonPhrase = parts[2]
            };
        }
        else
        {
            _currentMessage = new RtspRequest
            {
                Method = parts[0],
                RtspUri = new Uri(parts[1]),
                Version = parts[2]
            };
        }

        _state = RtspParserState.ReadingHeaders;
    }

    private bool ParseHeaderLine(string line)
    {
        // Empty line means end of headers.
        if (string.IsNullOrWhiteSpace(line))
        {
            return true;
        }

        int colonIndex = line.IndexOf(':');
        if (colonIndex == -1)
        {
            _state = RtspParserState.Error;
            return true;
        }

        string name = line[..colonIndex].Trim();
        string value = line[(colonIndex + 1)..].Trim();

        _currentMessage!.Headers[name] = value;

        if (name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
        {
            _contentLength = int.Parse(value);
            if (_contentLength is < 0 or > MaxContentLength)
            {
                _state = RtspParserState.Error;
                return true;
            }
        }

        return false;
    }

    private async Task<bool> ReadBodyAsync(Stream stream, CancellationToken cancellationToken)
    {
        if (_contentLength <= 0)
        {
            _currentMessage!.Data = ReadOnlyMemory<byte>.Empty;
            return true;
        }

        if (!await EnsureAvailableAsync(stream, _contentLength, cancellationToken))
        {
            return false;
        }

        _currentMessage!.Data = new ReadOnlyMemory<byte>(_buffer, _bufferStart, _contentLength);
        _bufferStart += _contentLength;

        if (_bufferStart == _bufferEnd)
        {
            _bufferStart = 0;
            _bufferEnd = 0;
        }

        return true;
    }

    private async Task<object?> ReadInterleavedFrameAsync(Stream stream, CancellationToken cancellationToken)
    {
        if (!await EnsureAvailableAsync(stream, 3, cancellationToken))
        {
            return null;
        }

        _interleavedChannel = _buffer[_bufferStart];
        _interleavedLength = BinaryPrimitives.ReadUInt16BigEndian(new ReadOnlySpan<byte>(_buffer, _bufferStart + 1, 2));
        _bufferStart += 3;

        if (_interleavedLength > MaxContentLength)
        {
            _state = RtspParserState.Error;
            return null;
        }

        if (!await EnsureAvailableAsync(stream, _interleavedLength, cancellationToken))
        {
            return null;
        }

        _interleavedBuffer = _bufferPool.Rent(_interleavedLength);
        Buffer.BlockCopy(_buffer, _bufferStart, _interleavedBuffer, 0, _interleavedLength);
        _bufferStart += _interleavedLength;

        if (_bufferStart == _bufferEnd)
        {
            _bufferStart = 0;
            _bufferEnd = 0;
        }

        // Transfer ownership of the interleaved buffer to the frame.
        byte[] payload = _interleavedBuffer;
        _interleavedBuffer = null;
        InterleavedFrame frame = new(_interleavedChannel, payload, _interleavedLength, _bufferPool);

        ResetInternal(preserveBufferedData: true);
        return frame;
    }

    private async Task<bool> EnsureAvailableAsync(Stream stream, int requiredBytes, CancellationToken cancellationToken)
    {
        while (_bufferEnd - _bufferStart < requiredBytes)
        {
            if (!await FillBufferAsync(stream, cancellationToken))
            {
                return false;
            }
        }

        return true;
    }

    private async Task<bool> FillBufferAsync(Stream stream, CancellationToken cancellationToken)
    {
        if (_bufferEnd == MaxContentLength)
        {
            if (_bufferStart == 0)
            {
                _state = RtspParserState.Error;
                return false;
            }

            CompactBuffer();
        }

        int free = MaxContentLength - _bufferEnd;
        if (free == 0)
        {
            return false;
        }

        int toRead = Math.Min(ReadChunkSize, free);
        int read = await stream.ReadAsync(_buffer.AsMemory(_bufferEnd, toRead), cancellationToken);
        if (read == 0)
        {
            return false;
        }

        _bufferEnd += read;
        return true;
    }

    private void CompactBuffer()
    {
        if (_bufferStart == 0)
        {
            return;
        }

        int remaining = _bufferEnd - _bufferStart;
        if (remaining > 0)
        {
            // Move remaining data to start of buffer.
            Buffer.BlockCopy(_buffer, _bufferStart, _buffer, 0, remaining);
        }

        _bufferStart = 0;
        _bufferEnd = remaining;
    }
}
