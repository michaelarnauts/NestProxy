using NestProxy.Rtsp.Buffers;

namespace NestProxy.Rtsp.Protocol;

/// <summary>
/// Represents an interleaved binary frame (RTP or RTCP data) within an RTSP stream.
/// Format: $ + channel (1 byte) + length (2 bytes, big-endian) + data.
/// </summary>
public sealed class InterleavedFrame : IDisposable
{
    private readonly LargeBufferPool? _bufferPool;
    private readonly byte[]? _pooledBuffer;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="InterleavedFrame"/> class from existing memory (sender-side).
    /// </summary>
    /// <param name="channel">The interleaved channel number.</param>
    /// <param name="data">The frame payload.</param>
    public InterleavedFrame(byte channel, ReadOnlyMemory<byte> data)
    {
        Channel = channel;
        Data = data;
    }

    internal InterleavedFrame(byte channel, byte[] buffer, int length, LargeBufferPool bufferPool)
    {
        Channel = channel;
        Data = new ReadOnlyMemory<byte>(buffer, 0, length);

        _pooledBuffer = buffer;
        _bufferPool = bufferPool;
    }

    /// <summary>
    /// Gets the channel number (0-255).
    /// </summary>
    public byte Channel { get; }

    /// <summary>
    /// Gets the frame data.
    /// </summary>
    public ReadOnlyMemory<byte> Data { get; }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_pooledBuffer != null && _bufferPool != null)
        {
            _bufferPool.Return(_pooledBuffer);
        }
    }
}
